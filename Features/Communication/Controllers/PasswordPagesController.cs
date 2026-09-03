using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Features.Media.Models;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector")]
[Route("PasswordPages")]
public class PasswordPagesController(ApplicationDbContext context, S3StorageService storage) : ControllerBase
{
    private static readonly string[] Statuses = ["نشطه", "مراجعه", "محظوره", "تحتاج تحقق"];
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const string EmailStoreImage = "/static/gmail.svg";

    [HttpGet("Create")]
    public async Task<IActionResult> Create(CancellationToken ct) => Ok(await Lookups(ct));

    [HttpPost("Create")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] PasswordPageRequest request, CancellationToken ct)
    {
        var companyId = await ResolveCompanyId(request.PasswordPageTypeId, request.ManufacturingCompanyId, request.PageName, ct);
        var error = await Validate(request, companyId, ct);
        if (error is not null) return BadRequest(new { success = false, message = error });
        S3StoredObject? stored;
        try { stored = await UploadImage(request.PageImage, request.PageImageDataUrl, ct); }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        var page = new StorePasswordPage
        {
            PageName = request.PageName.Trim(), PasswordPageTypeId = request.PasswordPageTypeId,
            PageImageUrl = stored?.PublicUrl, PageImageS3Key = stored?.S3Key, Email = Clean(request.Email),
            Password = request.Password.Trim(), PhoneNumber = Clean(request.PhoneNumber),
            ManufacturingCompanyId = companyId, PageStatus = NormalizeStatus(request.PageStatus),
            PageStatusName = Clean(request.PageStatusName), Tasks = Clean(request.Tasks), CreatedAt = IstanbulTimeHelper.Now,
            CreatedByUserId = CurrentUserId()
        };
        context.StorePasswordPages.Add(page);
        await context.SaveChangesAsync(ct);
        AddLog(page, "إنشاء", "الصفحة", null, "تم إنشاء الصفحة");
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم إنشاء الصفحة بنجاح", item = page });
    }

    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? pageName, int? pageTypeId, string? tasks, string? email,
        string? phone, string? pageStatus, CancellationToken ct)
    {
        var query = Filter(false, pageName, pageTypeId, tasks, email, phone, pageStatus);
        return Ok(new { pages = await Rows(query, ct), lookups = await Lookups(ct) });
    }

    [HttpGet("Store")]
    public async Task<IActionResult> Store(int id, CancellationToken ct)
    {
        var store = await context.ManufacturingCompanies.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (store is null) return NotFound();
        var pages = await Rows(context.StorePasswordPages.AsNoTracking().Where(x => !x.IsDeleted && x.ManufacturingCompanyId == id), ct);
        return Ok(new { store, pages, lookups = await Lookups(ct) });
    }

    [HttpGet("Trash")]
    public async Task<IActionResult> Trash(CancellationToken ct) => Ok(new
    {
        pages = await Rows(context.StorePasswordPages.AsNoTracking().Where(x => x.IsDeleted), ct),
        lookups = await Lookups(ct)
    });

    [HttpGet("History")]
    public async Task<IActionResult> History(CancellationToken ct) => Ok(await HistoryQuery(null).Take(500).ToListAsync(ct));

    [HttpGet("TrashItems")]
    public async Task<IActionResult> TrashItems(int? storeId, CancellationToken ct)
    {
        var query = context.StorePasswordPages.AsNoTracking().Where(x => x.IsDeleted);
        if (storeId is > 0) query = query.Where(x => x.ManufacturingCompanyId == storeId);
        var pages = await Rows(query, ct);
        return Ok(new { success = true, totalCount = pages.Count, statusCounts = Statuses.Select(s => new { status = s, count = pages.Count(x => x.PageStatus == s) }), items = pages });
    }

    [HttpGet("HistoryItems")]
    public async Task<IActionResult> HistoryItems(int? storeId, CancellationToken ct) =>
        Ok(new { success = true, items = await HistoryQuery(storeId).Take(500).ToListAsync(ct) });

    [HttpPost("Edit")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Edit([FromForm] PasswordPageEditRequest request, CancellationToken ct)
    {
        var page = await context.StorePasswordPages.FirstOrDefaultAsync(x => x.Id == request.Id && !x.IsDeleted, ct);
        if (page is null) return NotFound(new { success = false, message = "الصفحة غير موجودة" });
        var companyId = await ResolveCompanyId(request.PasswordPageTypeId, request.ManufacturingCompanyId, request.PageName, ct);
        var error = await Validate(request, companyId, ct);
        if (error is not null) return BadRequest(new { success = false, message = error });
        var oldImageKey = page.PageImageS3Key;
        S3StoredObject? newImage;
        try { newImage = await UploadImage(request.PageImage, request.PageImageDataUrl, ct); }
        catch (InvalidOperationException ex) { return BadRequest(new { success = false, message = ex.Message }); }
        Track(page, "اسم الصفحة", page.PageName, request.PageName.Trim());
        Track(page, "نوع الصفحة", page.PasswordPageTypeId.ToString(), request.PasswordPageTypeId.ToString());
        Track(page, "حالة الصفحة", page.PageStatus, NormalizeStatus(request.PageStatus));
        Track(page, "اسم حالة الصفحة", page.PageStatusName, Clean(request.PageStatusName));
        Track(page, "البريد الإلكتروني", page.Email, Clean(request.Email));
        Track(page, "كلمة السر", page.Password, request.Password.Trim());
        Track(page, "رقم الهاتف", page.PhoneNumber, Clean(request.PhoneNumber));
        Track(page, "المتجر", page.ManufacturingCompanyId.ToString(), companyId.ToString());
        Track(page, "المهام", page.Tasks, Clean(request.Tasks));
        if (request.RemovePageImage) Track(page, "صورة الصفحة", page.PageImageUrl, null);
        else if (newImage is not null) Track(page, "صورة الصفحة", page.PageImageUrl, newImage.PublicUrl);
        page.PageName = request.PageName.Trim(); page.PasswordPageTypeId = request.PasswordPageTypeId;
        page.Email = Clean(request.Email); page.Password = request.Password.Trim(); page.PhoneNumber = Clean(request.PhoneNumber);
        page.ManufacturingCompanyId = companyId; page.PageStatus = NormalizeStatus(request.PageStatus);
        page.PageStatusName = Clean(request.PageStatusName); page.Tasks = Clean(request.Tasks);
        if (request.RemovePageImage) { page.PageImageUrl = null; page.PageImageS3Key = null; }
        if (newImage is not null) { page.PageImageUrl = newImage.PublicUrl; page.PageImageS3Key = newImage.S3Key; }
        page.UpdatedAt = IstanbulTimeHelper.Now; page.UpdatedByUserId = CurrentUserId();
        await context.SaveChangesAsync(ct);
        if ((request.RemovePageImage || newImage is not null) && !string.IsNullOrWhiteSpace(oldImageKey)) await storage.DeleteAsync(oldImageKey, ct);
        return Ok(new { success = true, message = "تم تعديل الصفحة بنجاح", imageUrl = page.PageImageUrl });
    }

    [HttpPost("Delete")]
    public async Task<IActionResult> Delete([FromForm] int id, CancellationToken ct) => await SetDeleted(id, true, ct);

    [HttpPost("Restore")]
    public async Task<IActionResult> Restore([FromForm] int id, CancellationToken ct) => await SetDeleted(id, false, ct);

    [HttpPost("PermanentDelete")]
    public async Task<IActionResult> PermanentDelete([FromForm] int id, CancellationToken ct)
    {
        var page = await context.StorePasswordPages.FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted, ct);
        if (page is null) return NotFound(new { success = false, message = "الصفحة غير موجودة" });
        var key = page.PageImageS3Key;
        context.StorePasswordPages.Remove(page);
        await context.SaveChangesAsync(ct);
        if (!string.IsNullOrWhiteSpace(key)) await storage.DeleteAsync(key, ct);
        return Ok(new { success = true, message = $"تم حذف {page.PageName} نهائيًا" });
    }

    private IQueryable<StorePasswordPage> Filter(bool deleted, string? name, int? typeId, string? tasks, string? email, string? phone, string? status)
    {
        var query = context.StorePasswordPages.AsNoTracking().Where(x => x.IsDeleted == deleted);
        if (!string.IsNullOrWhiteSpace(name)) query = query.Where(x => x.PageName.Contains(name.Trim()));
        if (typeId is > 0) query = query.Where(x => x.PasswordPageTypeId == typeId);
        if (!string.IsNullOrWhiteSpace(tasks)) query = query.Where(x => x.Tasks != null && x.Tasks.Contains(tasks.Trim()));
        if (!string.IsNullOrWhiteSpace(email)) query = query.Where(x => x.Email != null && x.Email.Contains(email.Trim()));
        if (!string.IsNullOrWhiteSpace(phone)) query = query.Where(x => x.PhoneNumber != null && x.PhoneNumber.Contains(phone.Trim()));
        if (!string.IsNullOrWhiteSpace(status)) { var value = NormalizeStatus(status); query = query.Where(x => x.PageStatus == value); }
        return query;
    }

    private async Task<List<PasswordPageRow>> Rows(IQueryable<StorePasswordPage> query, CancellationToken ct) => await query
        .OrderByDescending(x => x.IsDeleted ? x.DeletedAt ?? x.CreatedAt : x.CreatedAt)
        .Select(x => new PasswordPageRow(x.Id, x.PageName, x.PageImageUrl, x.PasswordPageTypeId,
            x.PasswordPageType != null ? x.PasswordPageType.Name : "غير محدد", x.PageStatus, x.PageStatusName,
            x.Email, x.Password, x.PhoneNumber, x.ManufacturingCompanyId,
            x.ManufacturingCompany != null ? x.ManufacturingCompany.Name : "بدون متجر", x.Tasks, x.CreatedAt, x.UpdatedAt, x.IsDeleted, x.DeletedAt))
        .ToListAsync(ct);

    private IQueryable<PasswordPageChangeLog> HistoryQuery(int? storeId)
    {
        var query = context.PasswordPageChangeLogs.AsNoTracking();
        if (storeId is > 0) query = query.Where(x => x.StorePasswordPage != null && x.StorePasswordPage.ManufacturingCompanyId == storeId);
        return query.OrderByDescending(x => x.ChangedAt);
    }

    private async Task<object> Lookups(CancellationToken ct) => new
    {
        pageTypesInitialized = await EnsureDefaultPageTypes(ct),
        pageTypes = await context.PasswordPageTypes.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync(ct),
        statuses = Statuses,
        stores = await context.ManufacturingCompanies.AsNoTracking().Where(x => x.IsShown && !x.IsPasswordEmailStore).OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name, x.ImageUrl }).ToListAsync(ct)
    };

    private async Task<string?> Validate(PasswordPageRequest request, int companyId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.PageName) || string.IsNullOrWhiteSpace(request.Password)) return "اسم الصفحة وكلمة السر مطلوبين";
        if (!await context.PasswordPageTypes.AnyAsync(x => x.Id == request.PasswordPageTypeId && x.IsActive, ct)) return "نوع الصفحة غير صحيح";
        if (!await context.ManufacturingCompanies.AnyAsync(x => x.Id == companyId && x.IsShown, ct)) return "المتجر غير صحيح";
        if (request.PageImage is { Length: > MaxImageBytes }) return "حجم الصورة لا يزيد عن 5 ميجابايت";
        return null;
    }

    private async Task<bool> EnsureDefaultPageTypes(CancellationToken ct)
    {
        var defaults = new (string Name, string Icon)[]
        {
            ("فيسبوك", "facebook"), ("انستجرام", "instagram"), ("تيك توك", "tiktok"),
            ("واتساب", "whatsapp"), ("سناب شات", "snapchat"), ("البريد الإلكتروني", "gmail"),
            ("موقع إلكتروني", "website"), ("أخرى", "link")
        };
        var existing = await context.PasswordPageTypes.ToListAsync(ct);
        var changed = false;
        foreach (var item in defaults)
        {
            var row = existing.FirstOrDefault(x => x.Name == item.Name);
            if (row is null)
            {
                context.PasswordPageTypes.Add(new PasswordPageType { Name = item.Name, IconClass = item.Icon, IsActive = true });
                changed = true;
            }
            else
            {
                if (!row.IsActive) { row.IsActive = true; changed = true; }
                if (string.IsNullOrWhiteSpace(row.IconClass)) { row.IconClass = item.Icon; changed = true; }
            }
        }
        if (changed) await context.SaveChangesAsync(ct);
        return true;
    }

    private async Task<int> ResolveCompanyId(int pageTypeId, int requestedId, string pageName, CancellationToken ct)
    {
        await EnsureDefaultPageTypes(ct);
        var type = await context.PasswordPageTypes.AsNoTracking().Where(x => x.Id == pageTypeId && x.IsActive)
            .Select(x => new { x.Name, x.IconClass }).FirstOrDefaultAsync(ct);
        if (type is null || !IsEmailType(type.Name, type.IconClass)) return requestedId;
        var emailStore = await context.ManufacturingCompanies.FirstOrDefaultAsync(x => x.IsPasswordEmailStore && x.IsShown, ct);
        if (emailStore is not null) return emailStore.Id;
        emailStore = new ManufacturingCompany
        {
            Name = Clean(pageName) ?? "البريد الإلكتروني", ImageUrl = EmailStoreImage,
            ImageUrl2 = EmailStoreImage, IsShown = true, IsPasswordEmailStore = true
        };
        context.ManufacturingCompanies.Add(emailStore);
        await context.SaveChangesAsync(ct);
        return emailStore.Id;
    }

    private static bool IsEmailType(string? name, string? icon)
    {
        var n = (name ?? string.Empty).Trim().ToLowerInvariant();
        var i = (icon ?? string.Empty).Trim().ToLowerInvariant();
        return i.Contains("gmail") || i.Contains("email") || n.Contains("gmail") || n.Contains("email")
            || n.Contains("mail") || n.Contains("بريد") || n.Contains("ايميل") || n.Contains("إيميل")
            || n.Contains("الايميل") || n.Contains("الإيميل");
    }

    private async Task<S3StoredObject?> UploadImage(IFormFile? file, string? dataUrl, CancellationToken ct)
    {
        if (file is { Length: > 0 })
        {
            var extension = ResolveImageExtension(file.FileName, file.ContentType);
            if (extension is null) throw new InvalidOperationException("الصورة يجب أن تكون JPG أو PNG أو WEBP أو GIF");
            if (file.Length > MaxImageBytes) throw new InvalidOperationException("حجم الصورة لا يزيد عن 5 ميجابايت");
            return await storage.UploadAsync(file, "password-pages", CurrentUserId(), ct);
        }
        var value = dataUrl?.Trim();
        if (string.IsNullOrWhiteSpace(value)) return null;
        var comma = value.IndexOf(',');
        if (!value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase) || comma <= 0
            || !value[..comma].Contains(";base64", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("بيانات صورة الصفحة غير صحيحة");
        var contentType = value[5..comma].Split(';')[0].Trim();
        var dataExtension = ResolveImageExtension(null, contentType);
        if (dataExtension is null) throw new InvalidOperationException("الصورة يجب أن تكون JPG أو PNG أو WEBP أو GIF");
        byte[] bytes;
        try { bytes = Convert.FromBase64String(value[(comma + 1)..]); }
        catch (FormatException) { throw new InvalidOperationException("تعذر قراءة صورة الصفحة الملصقة"); }
        if (bytes.Length == 0) return null;
        if (bytes.LongLength > MaxImageBytes) throw new InvalidOperationException("حجم الصورة لا يزيد عن 5 ميجابايت");
        await using var stream = new MemoryStream(bytes, writable: false);
        return await storage.UploadStreamAsync(stream, bytes.LongLength, "password-pages", $"image{dataExtension}", contentType, CurrentUserId(), ct);
    }

    private static string? ResolveImageExtension(string? fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName ?? string.Empty).ToLowerInvariant();
        if (extension is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif") return extension == ".jpeg" ? ".jpg" : extension;
        return contentType?.Split(';')[0].Trim().ToLowerInvariant() switch
        {
            "image/jpeg" or "image/jpg" => ".jpg", "image/png" => ".png",
            "image/webp" => ".webp", "image/gif" => ".gif", _ => null
        };
    }

    private async Task<IActionResult> SetDeleted(int id, bool deleted, CancellationToken ct)
    {
        var page = await context.StorePasswordPages.FirstOrDefaultAsync(x => x.Id == id && x.IsDeleted != deleted, ct);
        if (page is null) return NotFound(new { success = false, message = "الصفحة غير موجودة" });
        page.IsDeleted = deleted; page.DeletedAt = deleted ? IstanbulTimeHelper.Now : null;
        page.DeletedByUserId = deleted ? CurrentUserId() : null; page.UpdatedAt = IstanbulTimeHelper.Now; page.UpdatedByUserId = CurrentUserId();
        AddLog(page, deleted ? "حذف مؤقت" : "استرداد", "الصفحة", deleted ? "فعالة" : "سلة المهملات", deleted ? "سلة المهملات" : "فعالة");
        await context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = deleted ? "تم نقل الصفحة إلى سلة المهملات" : "تم استرداد الصفحة" });
    }

    private void Track(StorePasswordPage page, string field, string? oldValue, string? newValue)
    {
        if (oldValue != newValue) AddLog(page, "تعديل", field, oldValue, newValue);
    }
    private void AddLog(StorePasswordPage page, string action, string field, string? oldValue, string? newValue) =>
        context.PasswordPageChangeLogs.Add(new PasswordPageChangeLog
        {
            StorePasswordPageId = page.Id, PageName = page.PageName, ActionType = action, FieldName = field,
            OldValue = oldValue, NewValue = newValue, ChangedAt = IstanbulTimeHelper.Now,
            ChangedByUserId = CurrentUserId(), ChangedByName = User.Identity?.Name
        });
    private string CurrentUserId() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string NormalizeStatus(string? value) => Statuses.Contains(value?.Trim()) ? value!.Trim() : "نشطه";

    public record PasswordPageRequest(string PageName, int PasswordPageTypeId, string Password, int ManufacturingCompanyId,
        string? PageStatus, string? PageStatusName, string? Email, string? PhoneNumber, string? Tasks, IFormFile? PageImage,
        string? PageImageDataUrl);
    public sealed record PasswordPageEditRequest(int Id, string PageName, int PasswordPageTypeId, string Password,
        int ManufacturingCompanyId, string? PageStatus, string? PageStatusName, string? Email, string? PhoneNumber,
        string? Tasks, IFormFile? PageImage, string? PageImageDataUrl, bool RemovePageImage)
        : PasswordPageRequest(PageName, PasswordPageTypeId, Password, ManufacturingCompanyId, PageStatus, PageStatusName, Email, PhoneNumber, Tasks, PageImage, PageImageDataUrl);
    private sealed record PasswordPageRow(int Id, string PageName, string? PageImageUrl, int PasswordPageTypeId,
        string PageTypeName, string PageStatus, string? PageStatusName, string? Email, string Password, string? PhoneNumber,
        int ManufacturingCompanyId, string StoreName, string? Tasks, DateTime CreatedAt, DateTime? UpdatedAt, bool IsDeleted, DateTime? DeletedAt);
}
