using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector")]
[Route("api/v1/marketing/video-links")]
[Route("VideoLinks")]
public class VideoLinksController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public VideoLinksController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/VideoLinks/Index")]
    public async Task<IActionResult> Index([FromQuery] int? manufacturingCompanyId, CancellationToken ct = default)
    {
        var query = _context.VideoLinks.AsNoTracking().AsQueryable();
        if (manufacturingCompanyId.HasValue)
            query = query.Where(v => v.ManufacturingCompanyId == manufacturingCompanyId.Value);
        query = query.Where(link => !link.IsDeleted);

        var rows = await query.Include(v => v.ManufacturingCompany).OrderByDescending(v => v.Id).ToListAsync(ct);
        var stores = await GetStores(ct);
        var usedStoreIds = rows.Select(item => item.ManufacturingCompanyId).ToHashSet();
        return Ok(new
        {
            stores,
            cardStores = stores.Where(item => usedStoreIds.Contains(item.Id)).ToList(),
            links = rows.Select(item => ToItem(item)).ToList()
        });
    }

    [HttpPost("Create")]
    [HttpPost("/VideoLinks/Create")]
    public async Task<IActionResult> Create([FromBody] VideoLinkUpsertRequest request, CancellationToken ct = default)
    {
        var store = await _context.ManufacturingCompanies.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.ManufacturingCompanyId, ct);
        if (store is null) return BadRequest(new { success = false, message = "المتجر غير موجود." });
        if (!TryNormalizeUrl(request.Url, out var normalizedUrl))
            return BadRequest(new { success = false, message = "اكتبي رابط صحيح." });
        var now = IstanbulTimeHelper.Now;
        var link = new VideoLink
        {
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            Url = normalizedUrl,
            CreatedAt = now,
            CreatedByUserId = User.GetUserId(),
            CreatedByName = User.Identity?.Name
        };

        await _context.VideoLinks.AddAsync(link, ct);
        await _context.SaveChangesAsync(ct);
        _context.VideoLinkChangeHistories.Add(NewHistory(link.Id, "Create", null, store.Id, null, store.Name, null, link.Url, now));
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم إضافة الرابط بنجاح.", item = ToItem(link, store) });
    }

    [HttpPost("Edit")]
    [HttpPost("/VideoLinks/Edit")]
    public async Task<IActionResult> Edit([FromBody] VideoLinkUpsertRequest request, CancellationToken ct = default)
    {
        var link = await _context.VideoLinks.Include(v => v.ManufacturingCompany)
            .FirstOrDefaultAsync(v => v.Id == request.Id && !v.IsDeleted, ct);
        if (link == null) return NotFound("Video link not found.");
        var store = await _context.ManufacturingCompanies.AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == request.ManufacturingCompanyId, ct);
        if (store is null) return BadRequest(new { success = false, message = "المتجر غير موجود." });
        if (!TryNormalizeUrl(request.Url, out var normalizedUrl))
            return BadRequest(new { success = false, message = "اكتبي رابط صحيح." });
        if (link.ManufacturingCompanyId == store.Id && string.Equals(link.Url, normalizedUrl, StringComparison.OrdinalIgnoreCase))
            return Ok(new { success = true, message = "لا توجد تعديلات جديدة.", item = link });

        var oldStoreId = link.ManufacturingCompanyId;
        var oldStoreName = link.ManufacturingCompany?.Name;
        var oldUrl = link.Url;
        var now = IstanbulTimeHelper.Now;
        link.ManufacturingCompanyId = request.ManufacturingCompanyId;
        link.Url = normalizedUrl;
        link.UpdatedAt = now;
        link.UpdatedByUserId = User.GetUserId();
        link.UpdatedByName = User.Identity?.Name;
        _context.VideoLinkChangeHistories.Add(NewHistory(link.Id, "Edit", oldStoreId, store.Id, oldStoreName, store.Name, oldUrl, normalizedUrl, now));
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم تعديل الرابط بنجاح.", item = ToItem(link, store) });
    }

    [HttpPost("/VideoLinks/Delete")]
    public Task<IActionResult> Delete([FromForm] int id, CancellationToken ct = default) => DeleteCore(id, ct);

    [HttpPost("Delete/{id:int}")]
    [HttpDelete("{id:int}")]
    public Task<IActionResult> DeleteById([RouteOrRequest] int id, CancellationToken ct = default) => DeleteCore(id, ct);

    private async Task<IActionResult> DeleteCore(int id, CancellationToken ct)
    {
        var link = await _context.VideoLinks.Include(v => v.ManufacturingCompany)
            .FirstOrDefaultAsync(v => v.Id == id && !v.IsDeleted, ct);
        if (link == null) return NotFound("Video link not found.");
        var now = IstanbulTimeHelper.Now;
        link.IsDeleted = true;
        link.DeletedAt = now;
        link.DeletedByUserId = User.GetUserId();
        link.DeletedByName = User.Identity?.Name;
        _context.VideoLinkChangeHistories.Add(NewHistory(link.Id, "Delete", link.ManufacturingCompanyId, null,
            link.ManufacturingCompany?.Name, null, link.Url, null, now));
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("/VideoLinks/Trash")]
    public async Task<IActionResult> Trash(int? storeId, CancellationToken ct)
    {
        var items = await GetTrashItems(storeId, ct);
        return Ok(new { stores = await GetStores(ct), links = items, storeFilterId = storeId });
    }

    [HttpPost("/VideoLinks/Restore")]
    public async Task<IActionResult> Restore([FromForm] int id, CancellationToken ct)
    {
        var link = await _context.VideoLinks.Include(item => item.ManufacturingCompany)
            .FirstOrDefaultAsync(item => item.Id == id && item.IsDeleted, ct);
        if (link is null) return NotFound(new { success = false, message = "الرابط غير موجود في المحذوفات." });
        var now = IstanbulTimeHelper.Now;
        link.IsDeleted = false;
        link.DeletedAt = null;
        link.DeletedByUserId = null;
        link.DeletedByName = null;
        link.UpdatedAt = now;
        link.UpdatedByUserId = User.GetUserId();
        link.UpdatedByName = User.Identity?.Name;
        _context.VideoLinkChangeHistories.Add(NewHistory(link.Id, "Restore", null, link.ManufacturingCompanyId,
            null, link.ManufacturingCompany?.Name, null, link.Url, now));
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم استرداد الرابط بنجاح.", item = ToItem(link) });
    }

    [HttpGet("/VideoLinks/TrashData")]
    public async Task<IActionResult> TrashData(int? storeId, CancellationToken ct) =>
        Ok(new { success = true, items = await GetTrashItems(storeId, ct) });

    [HttpGet("/VideoLinks/HistoryData")]
    public async Task<IActionResult> HistoryData(int? storeId, CancellationToken ct) =>
        Ok(new { success = true, items = await GetHistory(storeId, 500, ct) });

    [HttpGet("/VideoLinks/History")]
    public async Task<IActionResult> History(int? storeId, CancellationToken ct) =>
        Ok(new { stores = await GetStores(ct), history = await GetHistory(storeId, null, ct), storeFilterId = storeId });

    private async Task<List<VideoStoreOption>> GetStores(CancellationToken ct)
    {
        var rows = await _context.ManufacturingCompanies.AsNoTracking().OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Name, item.ImageUrl }).ToListAsync(ct);
        return rows.Select(item => new VideoStoreOption(item.Id,
            string.IsNullOrWhiteSpace(item.Name) ? $"متجر رقم {item.Id}" : item.Name.Trim(),
            NormalizeImage(item.ImageUrl))).ToList();
    }

    private async Task<List<object>> GetTrashItems(int? storeId, CancellationToken ct)
    {
        var query = _context.VideoLinks.AsNoTracking().Where(item => item.IsDeleted);
        if (storeId.HasValue) query = query.Where(item => item.ManufacturingCompanyId == storeId);
        var rows = await query.OrderByDescending(item => item.DeletedAt).Select(item => new
        {
            item.Id,
            item.ManufacturingCompanyId,
            StoreName = item.ManufacturingCompany != null ? item.ManufacturingCompany.Name : string.Empty,
            StoreImageUrl = item.ManufacturingCompany != null ? item.ManufacturingCompany.ImageUrl : null,
            item.Url,
            DeletedAt = FormatArabicDate(item.DeletedAt),
            item.DeletedByName
        }).ToListAsync(ct);
        return rows.Select(item => (object)new
        {
            item.Id,
            item.ManufacturingCompanyId,
            StoreName = string.IsNullOrWhiteSpace(item.StoreName) ? $"متجر رقم {item.ManufacturingCompanyId}" : item.StoreName.Trim(),
            StoreImageUrl = NormalizeImage(item.StoreImageUrl),
            item.Url,
            item.DeletedAt,
            DeletedByName = item.DeletedByName ?? string.Empty
        }).ToList();
    }

    private async Task<List<object>> GetHistory(int? storeId, int? take, CancellationToken ct)
    {
        var query = _context.VideoLinkChangeHistories.AsNoTracking();
        if (storeId.HasValue) query = query.Where(item => item.OldManufacturingCompanyId == storeId || item.NewManufacturingCompanyId == storeId);
        var ordered = query.OrderByDescending(item => item.ChangedAt);
        var rows = await (take.HasValue ? ordered.Take(take.Value) : ordered).ToListAsync(ct);
        return rows.Select(item => (object)new
        {
            item.Id,
            item.VideoLinkId,
            item.Action,
            item.OldManufacturingCompanyId,
            item.NewManufacturingCompanyId,
            OldStoreName = item.OldStoreName ?? string.Empty,
            NewStoreName = item.NewStoreName ?? string.Empty,
            OldUrl = item.OldUrl ?? string.Empty,
            NewUrl = item.NewUrl ?? string.Empty,
            ChangedAt = FormatArabicDate(item.ChangedAt),
            ChangedByName = item.ChangedByName ?? string.Empty
        }).ToList();
    }

    private VideoLinkChangeHistory NewHistory(int linkId, string action, int? oldStoreId, int? newStoreId,
        string? oldStoreName, string? newStoreName, string? oldUrl, string? newUrl, DateTime now) => new()
    {
        VideoLinkId = linkId,
        Action = action,
        OldManufacturingCompanyId = oldStoreId,
        NewManufacturingCompanyId = newStoreId,
        OldStoreName = oldStoreName,
        NewStoreName = newStoreName,
        OldUrl = oldUrl,
        NewUrl = newUrl,
        ChangedAt = now,
        ChangedByUserId = User.GetUserId(),
        ChangedByName = User.Identity?.Name
    };

    private static bool TryNormalizeUrl(string? input, out string normalized)
    {
        var value = input?.Trim() ?? string.Empty;
        if (!value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !value.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            value = "https://" + value;
        if (Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            normalized = uri.AbsoluteUri;
            return true;
        }
        normalized = string.Empty;
        return false;
    }

    private static string NormalizeImage(string? image)
    {
        var value = image?.Trim().Replace('\\', '/') ?? string.Empty;
        if (value.Length == 0 || value.Equals("null", StringComparison.OrdinalIgnoreCase) || value.Equals("undefined", StringComparison.OrdinalIgnoreCase))
            return "/static/DefaultImage.svg";
        if (value.StartsWith("~/", StringComparison.Ordinal)) return value[1..];
        if (value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || value.StartsWith('/')) return value;
        return "/" + value.TrimStart('/');
    }

    private static object ToItem(VideoLink link, ManufacturingCompany? store = null)
    {
        var company = store ?? link.ManufacturingCompany;
        return new
        {
            link.Id,
            link.ManufacturingCompanyId,
            StoreName = string.IsNullOrWhiteSpace(company?.Name) ? $"متجر رقم {link.ManufacturingCompanyId}" : company.Name.Trim(),
            StoreImageUrl = NormalizeImage(company?.ImageUrl),
            link.Url,
            CreatedAt = FormatArabicDate(link.CreatedAt)
        };
    }

    private static string FormatArabicDate(DateTime? value)
    {
        if (!value.HasValue) return string.Empty;
        var suffix = value.Value.Hour < 12 ? "ص" : "م";
        return value.Value.ToString("yyyy/MM/dd hh:mm", CultureInfo.InvariantCulture) + " " + suffix;
    }

    private sealed record VideoStoreOption(int Id, string Name, string ImageUrl);
}

public sealed record VideoLinkUpsertRequest(int? Id, int ManufacturingCompanyId, string Url);
