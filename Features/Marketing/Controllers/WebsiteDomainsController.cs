using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
[Route("api/v1/marketing/domains")]
[Route("WebsiteDomains")]
public class WebsiteDomainsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet]
    [HttpGet("GetDomains")]
    public async Task<IActionResult> GetDomains(CancellationToken ct) =>
        Ok(await context.WebsiteDomains.AsNoTracking().OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.Id).ToListAsync(ct));

    [HttpGet("/WebsiteDomains/Index")]
    public async Task<IActionResult> Index(string? domain, int? manufacturingCompanyId, string? status, CancellationToken ct) =>
        Ok(new
        {
            stores = await GetStores(ct),
            domainOptions = await GetDomainOptions(ct),
            items = await GetItems(domain, manufacturingCompanyId, status, false, ct),
            filters = new { domain, manufacturingCompanyId, status }
        });

    [HttpGet("/WebsiteDomains/List")]
    public async Task<IActionResult> List(string? domain, int? manufacturingCompanyId, string? status, CancellationToken ct)
    {
        var items = await GetItems(domain, manufacturingCompanyId, status, false, ct);
        return Ok(new { success = true, count = items.Count, items });
    }

    [HttpGet("/WebsiteDomains/Trash")]
    public async Task<IActionResult> Trash(CancellationToken ct)
    {
        var items = await GetItems(null, null, null, true, ct);
        return Ok(new { success = true, count = items.Count, items });
    }

    [HttpGet("/WebsiteDomains/EditLogs")]
    public async Task<IActionResult> EditLogs(CancellationToken ct)
    {
        var raw = await context.WebsiteDomainEditLogs.AsNoTracking()
            .OrderByDescending(x => x.EditedAt).ThenByDescending(x => x.Id).Take(80)
            .ToListAsync(ct);
        var companyIds = raw.SelectMany(x => new[] { x.OldManufacturingCompanyId, x.NewManufacturingCompanyId }).Distinct().ToList();
        var names = await context.ManufacturingCompanies.AsNoTracking().Where(x => companyIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Name, ct);
        var items = raw.Select(x => new
        {
            id = x.Id,
            websiteDomainId = x.WebsiteDomainId,
            oldDomain = x.OldDomain,
            newDomain = x.NewDomain,
            oldStoreName = names.GetValueOrDefault(x.OldManufacturingCompanyId, "-"),
            newStoreName = names.GetValueOrDefault(x.NewManufacturingCompanyId, "-"),
            oldIsActive = x.OldIsActive,
            newIsActive = x.NewIsActive,
            editedAt = x.EditedAt,
            isRestored = x.IsRestored,
            restoredAt = x.RestoredAt
        }).ToList();
        return Ok(new { success = true, count = items.Count, items });
    }

    [HttpGet("/WebsiteDomains/DomainOptions")]
    public async Task<IActionResult> DomainOptions(CancellationToken ct) =>
        Ok(new { success = true, domains = await GetDomainOptions(ct) });

    [HttpGet("/WebsiteDomains/Details")]
    public async Task<IActionResult> Details(int id, CancellationToken ct)
    {
        var item = await GetItem(id, ct);
        return item is null ? Fail("الموقع غير موجود.") : Ok(new { success = true, item });
    }

    [HttpPost("/WebsiteDomains/Create")]
    public async Task<IActionResult> Create([FromForm] WebsiteDomainInput input, CancellationToken ct)
    {
        var domain = NormalizeDomain(input.Domain);
        if (domain.Length == 0) return Fail("اكتب الدومين.");
        if (input.ManufacturingCompanyId <= 0) return Fail("اختر المتجر.");
        if (!await context.ManufacturingCompanies.AnyAsync(x => x.Id == input.ManufacturingCompanyId, ct)) return Fail("المتجر غير موجود.");
        if (await DuplicateExists(domain, input.ManufacturingCompanyId, null, ct)) return Fail("الدومين مضاف لنفس المتجر قبل كده.");
        context.WebsiteDomains.Add(new WebsiteDomain
        {
            Domain = domain,
            ManufacturingCompanyId = input.ManufacturingCompanyId,
            IsActive = input.IsActive,
            CreatedAt = IstanbulTimeHelper.Now
        });
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم الانشاء", domain });
    }

    [HttpPost("/WebsiteDomains/Edit")]
    public async Task<IActionResult> Edit([FromForm] WebsiteDomainEditInput input, CancellationToken ct)
    {
        var domain = NormalizeDomain(input.Domain);
        if (input.Id <= 0) return Fail("الموقع غير صحيح.");
        if (domain.Length == 0) return Fail("اكتب الدومين.");
        if (input.ManufacturingCompanyId <= 0) return Fail("اختر المتجر.");
        var item = await context.WebsiteDomains.FirstOrDefaultAsync(x => x.Id == input.Id && !x.IsDeleted, ct);
        if (item is null) return Fail("الموقع غير موجود.");
        if (!await context.ManufacturingCompanies.AnyAsync(x => x.Id == input.ManufacturingCompanyId, ct)) return Fail("المتجر غير موجود.");
        if (await DuplicateExists(domain, input.ManufacturingCompanyId, input.Id, ct)) return Fail("الدومين مضاف لنفس المتجر قبل كده.");

        var now = IstanbulTimeHelper.Now;
        context.WebsiteDomainEditLogs.Add(new WebsiteDomainEditLog
        {
            WebsiteDomainId = item.Id,
            OldDomain = item.Domain,
            NewDomain = domain,
            OldManufacturingCompanyId = item.ManufacturingCompanyId,
            NewManufacturingCompanyId = input.ManufacturingCompanyId,
            OldIsActive = item.IsActive,
            NewIsActive = input.IsActive,
            EditedAt = now,
            EditedByUserId = CurrentUserId()
        });
        item.Domain = domain;
        item.ManufacturingCompanyId = input.ManufacturingCompanyId;
        item.IsActive = input.IsActive;
        item.UpdatedAt = now;
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم التعديل", domain });
    }

    [HttpPost("/WebsiteDomains/Delete")]
    public async Task<IActionResult> Delete([FromForm] int id, CancellationToken ct)
    {
        var item = await context.WebsiteDomains.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (item is null) return Fail("الموقع غير موجود.");
        var now = IstanbulTimeHelper.Now;
        item.IsDeleted = true;
        item.IsPinned = false;
        item.DeletedAt = now;
        item.UpdatedAt = now;
        item.DeletedByUserId = CurrentUserId();
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم النقل إلى سلة المهملات" });
    }

    [HttpPost("/WebsiteDomains/RestoreDeleted")]
    public async Task<IActionResult> RestoreDeleted([FromForm] int id, CancellationToken ct)
    {
        var item = await context.WebsiteDomains.FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted, ct);
        if (item is null) return Fail("الموقع غير موجود في سلة المهملات.");
        item.IsDeleted = false;
        item.DeletedAt = null;
        item.DeletedByUserId = string.Empty;
        item.UpdatedAt = IstanbulTimeHelper.Now;
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم الاسترداد" });
    }

    [HttpPost("/WebsiteDomains/RestoreEdit")]
    public async Task<IActionResult> RestoreEdit([FromForm] int id, CancellationToken ct)
    {
        var log = await context.WebsiteDomainEditLogs.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (log is null) return Fail("سجل التعديل غير موجود.");
        if (log.IsRestored) return Fail("تم استرداد هذا التعديل قبل كده.");
        var item = await context.WebsiteDomains.FirstOrDefaultAsync(x => x.Id == log.WebsiteDomainId && !x.IsDeleted, ct);
        if (item is null) return Fail("الموقع محذوف. استرده من سلة المهملات أولًا.");
        if (await DuplicateExists(log.OldDomain, log.OldManufacturingCompanyId, item.Id, ct))
            return Fail("لا يمكن الاسترداد لأن الدومين القديم موجود لنفس المتجر.");
        var now = IstanbulTimeHelper.Now;
        item.Domain = log.OldDomain;
        item.ManufacturingCompanyId = log.OldManufacturingCompanyId;
        item.IsActive = log.OldIsActive;
        item.UpdatedAt = now;
        log.IsRestored = true;
        log.RestoredAt = now;
        log.RestoredByUserId = CurrentUserId();
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم استرداد التعديل" });
    }

    [HttpPost("/WebsiteDomains/TogglePin")]
    public async Task<IActionResult> TogglePin([FromForm] int id, CancellationToken ct)
    {
        var item = await context.WebsiteDomains.FirstOrDefaultAsync(x => x.Id == id && !x.IsDeleted, ct);
        if (item is null) return Fail("الموقع غير موجود.");
        item.IsPinned = !item.IsPinned;
        item.UpdatedAt = IstanbulTimeHelper.Now;
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = item.IsPinned ? "تم التثبيت" : "تم إلغاء التثبيت", isPinned = item.IsPinned });
    }

    private async Task<List<WebsiteDomainRow>> GetItems(string? domain, int? companyId, string? status, bool deletedOnly, CancellationToken ct)
    {
        var cleanDomain = NormalizeDomain(domain);
        var cleanStatus = status?.Trim().ToLowerInvariant();
        var query = context.WebsiteDomains.AsNoTracking().Where(x => x.IsDeleted == deletedOnly);
        if (!deletedOnly)
        {
            if (cleanDomain.Length > 0) query = query.Where(x => x.Domain == cleanDomain);
            if (companyId is > 0) query = query.Where(x => x.ManufacturingCompanyId == companyId);
            if (cleanStatus == "active") query = query.Where(x => x.IsActive);
            else if (cleanStatus == "inactive") query = query.Where(x => !x.IsActive);
        }
        return await query.OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.Id)
            .Select(x => new WebsiteDomainRow(x.Id, x.Domain, x.ManufacturingCompanyId,
                x.ManufacturingCompany != null ? x.ManufacturingCompany.Name : "-",
                NormalizeImageUrl(x.ManufacturingCompany != null ? x.ManufacturingCompany.ImageUrl : null),
                x.IsActive, x.IsPinned)).ToListAsync(ct);
    }

    private async Task<WebsiteDomainRow?> GetItem(int id, CancellationToken ct) => await context.WebsiteDomains.AsNoTracking()
        .Where(x => x.Id == id && !x.IsDeleted)
        .Select(x => new WebsiteDomainRow(x.Id, x.Domain, x.ManufacturingCompanyId,
            x.ManufacturingCompany != null ? x.ManufacturingCompany.Name : "-",
            NormalizeImageUrl(x.ManufacturingCompany != null ? x.ManufacturingCompany.ImageUrl : null),
            x.IsActive, x.IsPinned)).FirstOrDefaultAsync(ct);

    private async Task<List<StoreOption>> GetStores(CancellationToken ct) => await context.ManufacturingCompanies.AsNoTracking()
        .OrderBy(x => x.Name).Select(x => new StoreOption(x.Id, x.Name, NormalizeImageUrl(x.ImageUrl))).ToListAsync(ct);
    private async Task<List<string>> GetDomainOptions(CancellationToken ct) => await context.WebsiteDomains.AsNoTracking()
        .Where(x => !x.IsDeleted && x.Domain != string.Empty).Select(x => x.Domain).Distinct().OrderBy(x => x).ToListAsync(ct);
    private Task<bool> DuplicateExists(string domain, int companyId, int? exceptId, CancellationToken ct) =>
        context.WebsiteDomains.AnyAsync(x => !x.IsDeleted && x.ManufacturingCompanyId == companyId
            && x.Domain == domain && (!exceptId.HasValue || x.Id != exceptId), ct);
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private OkObjectResult Fail(string message) => Ok(new { success = false, message });
    private static string NormalizeDomain(string? value)
    {
        var domain = value?.Trim() ?? string.Empty;
        if (domain.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) domain = domain[8..];
        else if (domain.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) domain = domain[7..];
        return domain.TrimEnd('/');
    }
    private static string NormalizeImageUrl(string? value)
    {
        var image = value?.Trim() ?? string.Empty;
        if (image.Length == 0 || image.Equals("null", StringComparison.OrdinalIgnoreCase)) return "/static/DefaultImage.svg";
        if (image.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || image.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || image.StartsWith('/') || image.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) return image;
        return "/" + image.TrimStart('/');
    }

    public record WebsiteDomainInput(string? Domain, int ManufacturingCompanyId, bool IsActive = true);
    public sealed record WebsiteDomainEditInput(int Id, string? Domain, int ManufacturingCompanyId, bool IsActive = true)
        : WebsiteDomainInput(Domain, ManufacturingCompanyId, IsActive);
    private sealed record WebsiteDomainRow(int Id, string Domain, int ManufacturingCompanyId, string StoreName,
        string StoreImageUrl, bool IsActive, bool IsPinned);
    private sealed record StoreOption(int Id, string Name, string ImageUrl);
}
