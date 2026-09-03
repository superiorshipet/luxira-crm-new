using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Luxira.Api.Infrastructure.S3;
using System.Text.Json;

namespace Luxira.Api.Features.ManufacturingCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/product-images")]
[Route("ProductImages")]
public class ProductImagesController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _storage;

    public ProductImagesController(ApplicationDbContext context, S3StorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    [HttpGet("by-product/{productId:int}")]
    [HttpGet("/ProductImages/GetImages/{productId:int}")]
    public async Task<ActionResult<List<ProductImage>>> GetProductImages(
        [RouteOrRequest] int productId,
        CancellationToken ct)
    {
        var product = await _context.MainProducts.AsNoTracking()
            .Where(item => item.Id == productId)
            .Select(item => new { item.Name, item.ManufacturingCompanyId })
            .FirstOrDefaultAsync(ct);
        if (product is null) throw new NotFoundException("Product not found.");

        var images = await _context.ProductImages
            .AsNoTracking()
            .Where(image => image.ManufacturingCompanyId == product.ManufacturingCompanyId
                && image.ProductName == product.Name)
            .OrderByDescending(image => image.CreatedAt)
            .ToListAsync(ct);

        return Ok(images);
    }

    [HttpPost]
    [HttpPost("/ProductImages/AddImage")]
    public async Task<ActionResult<ProductImage>> AddImage([FromBody] AddProductImageRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.ImageUrl))
            throw new BadRequestException("Image URL is required.");
        if (string.IsNullOrWhiteSpace(request.ProductName))
            throw new BadRequestException("Product name is required.");
        if (!await _context.ManufacturingCompanies.AsNoTracking()
                .AnyAsync(company => company.Id == request.ManufacturingCompanyId, ct))
            throw new NotFoundException("Manufacturing company not found.");

        var img = new ProductImage
        {
            ImageUrl = request.ImageUrl.Trim(),
            ProductName = request.ProductName.Trim(),
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            CreatedAt = IstanbulTimeHelper.Now,
            CreatedByUserId = User.GetUserId(),
            CreatedByName = User.Identity?.Name
        };

        await _context.ProductImages.AddAsync(img, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(img);
    }

    [HttpGet("CreateImage")]
    [HttpGet("CreateVideo")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> CreateMedia(CancellationToken ct) => Ok(new
    {
        manufacturingCompanies = await GetCompanyOptionsAsync(ct),
        addedItems = await GetDraftItemsAsync(ct),
        products = await _context.MainWarehouses.AsNoTracking()
            .Where(item => item.Name != "").OrderBy(item => item.Name)
            .Select(item => new { item.Id, item.Name }).ToListAsync(ct),
    });

    [HttpPost("CreateImage")]
    [HttpPost("CreateImageAjax")]
    [HttpPost("CreateVideo")]
    [HttpPost("CreateVideoAjax")]
    [RequestSizeLimit(150_000_000)]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> CreateMedia(
        [FromForm] ProductImageCreateForm form,
        [FromForm] List<int>? manufacturingCompanyIds,
        CancellationToken ct)
    {
        var companyIds = (manufacturingCompanyIds ?? [])
            .Append(form.Input.ManufacturingCompanyId)
            .Where(id => id > 0).Distinct().ToList();
        var validation = await ValidateMediaInputAsync(form.Input, companyIds, mediaRequired: true, ct);
        if (validation is not null)
            return Ok(new { success = false, message = validation, items = await GetDraftItemsAsync(ct) });
        var userId = User.GetUserId()!;
        foreach (var companyId in companyIds)
        {
            _context.ProductImageDrafts.Add(new ProductImageDraft
            {
                ImageUrl = await SaveMediaInputAsync(form.Input.ProductImageBase64, ct),
                ProductName = form.Input.ProductName.Trim(),
                ManufacturingCompanyId = companyId,
                CreatedAt = IstanbulTimeHelper.Now,
                CreatedByUserId = userId,
                CreatedByName = User.Identity?.Name,
            });
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            message = companyIds.Count == 1
                ? "تمت إضافة الميديا للمتجر في الجدول المؤقت"
                : $"تمت إضافة الميديا إلى {companyIds.Count} متاجر في الجدول المؤقت",
            addedStoresCount = companyIds.Count,
            items = await GetDraftItemsAsync(ct),
        });
    }

    [HttpPost("UpdateTempImage")]
    [HttpPost("UpdateTempImageAjax")]
    [HttpPost("UpdateTempVideo")]
    [HttpPost("UpdateTempVideoAjax")]
    [RequestSizeLimit(150_000_000)]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> UpdateTempMedia([FromForm] ProductImageInputForm input, CancellationToken ct)
    {
        var draft = await _context.ProductImageDrafts
            .FirstOrDefaultAsync(item => item.Id == input.Id && item.CreatedByUserId == User.GetUserId(), ct);
        if (draft is null)
            return Ok(new { success = false, message = "لم يتم العثور على الصف المطلوب.", items = await GetDraftItemsAsync(ct) });
        var validation = await ValidateMediaInputAsync(input, [input.ManufacturingCompanyId], mediaRequired: false, ct);
        if (validation is not null)
            return Ok(new { success = false, message = validation, items = await GetDraftItemsAsync(ct) });
        draft.ProductName = input.ProductName.Trim();
        draft.ManufacturingCompanyId = input.ManufacturingCompanyId;
        if (!string.IsNullOrWhiteSpace(input.ProductImageBase64))
            draft.ImageUrl = await SaveMediaInputAsync(input.ProductImageBase64, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تم تعديل الصف بنجاح", items = await GetDraftItemsAsync(ct) });
    }

    [HttpPost("DeleteTempImage")]
    [HttpPost("DeleteTempImageAjax")]
    [HttpPost("DeleteTempVideo")]
    [HttpPost("DeleteTempVideoAjax")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> DeleteTempMedia([FromForm] int id, CancellationToken ct)
    {
        var draft = await _context.ProductImageDrafts
            .FirstOrDefaultAsync(item => item.Id == id && item.CreatedByUserId == User.GetUserId(), ct);
        if (draft is null)
            return Ok(new { success = false, message = "لم يتم العثور على الصف المطلوب.", items = await GetDraftItemsAsync(ct) });
        _context.ProductImageDrafts.Remove(draft);
        await _context.SaveChangesAsync(ct);
        await DeleteManagedMediaAsync(draft.ImageUrl, ct);
        return Ok(new { success = true, message = "تم حذف الصف بنجاح", items = await GetDraftItemsAsync(ct) });
    }

    [HttpPost("ApproveAll")]
    [HttpPost("ApproveAllAjax")]
    [HttpPost("SaveAllTempVideos")]
    [HttpPost("SaveAllTempVideosAjax")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> ApproveAllDrafts(CancellationToken ct)
    {
        var drafts = await _context.ProductImageDrafts
            .Where(item => item.CreatedByUserId == User.GetUserId())
            .OrderBy(item => item.CreatedAt).ToListAsync(ct);
        if (drafts.Count == 0)
            return Ok(new { success = false, message = "لا يوجد صور في الجدول المؤقت.", items = drafts });
        _context.ProductImages.AddRange(drafts.Select(draft => new ProductImage
        {
            ImageUrl = draft.ImageUrl,
            ProductName = draft.ProductName,
            ManufacturingCompanyId = draft.ManufacturingCompanyId,
            CreatedAt = IstanbulTimeHelper.Now,
            CreatedByUserId = draft.CreatedByUserId,
            CreatedByName = draft.CreatedByName,
        }));
        _context.ProductImageDrafts.RemoveRange(drafts);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, message = "تمت الموافقة على الكل وإضافتهم للعرض.", redirectUrl = "/ProductImages/ViewImage", items = Array.Empty<object>() });
    }

    [HttpPost("DeleteAll")]
    [HttpPost("DeleteAllAjax")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> DeleteAllDrafts(CancellationToken ct)
    {
        var drafts = await _context.ProductImageDrafts
            .Where(item => item.CreatedByUserId == User.GetUserId()).ToListAsync(ct);
        if (drafts.Count == 0)
            return Ok(new { success = false, message = "لا يوجد صور في الجدول المؤقت.", items = drafts });
        _context.ProductImageDrafts.RemoveRange(drafts);
        await _context.SaveChangesAsync(ct);
        foreach (var draft in drafts) await DeleteManagedMediaAsync(draft.ImageUrl, ct);
        return Ok(new { success = true, message = "تم حذف الكل بنجاح.", items = Array.Empty<object>() });
    }

    [HttpGet("ViewImage")]
    public async Task<IActionResult> ViewImage(
        [FromQuery] int? manufacturingCompanyId,
        [FromQuery] string? productName,
        CancellationToken ct)
    {
        var accessibleIds = await GetAccessibleCompanyIdsAsync(ct);
        var userId = User.GetUserId()!;
        var query = _context.ProductImages.AsNoTracking().AsQueryable();
        if (accessibleIds is not null)
            query = accessibleIds.Count == 0 ? query.Where(_ => false) : query.Where(item => accessibleIds.Contains(item.ManufacturingCompanyId));
        if (manufacturingCompanyId.HasValue)
            query = accessibleIds is null || accessibleIds.Contains(manufacturingCompanyId.Value)
                ? query.Where(item => item.ManufacturingCompanyId == manufacturingCompanyId.Value)
                : query.Where(_ => false);
        if (!string.IsNullOrWhiteSpace(productName)) query = query.Where(item => item.ProductName == productName);

        var items = await query
            .Select(item => new
            {
                item.Id,
                item.ImageUrl,
                item.ProductName,
                item.ManufacturingCompanyId,
                manufacturingCompanyName = item.ManufacturingCompany != null ? item.ManufacturingCompany.Name : string.Empty,
                manufacturingCompanyImageUrl = item.ManufacturingCompany != null ? item.ManufacturingCompany.ImageUrl ?? string.Empty : string.Empty,
                item.CreatedAt,
                item.CopyCount,
                item.LastCopiedAt,
                pin = _context.ProductImageUserPins
                    .Where(pin => pin.ProductImageId == item.Id && pin.ApplicationUserId == userId)
                    .Select(pin => (DateTime?)pin.PinnedAt)
                    .FirstOrDefault(),
            })
            .OrderByDescending(item => item.CopyCount >= 3 ? item.CopyCount : 0)
            .ThenByDescending(item => item.CopyCount >= 3 ? item.LastCopiedAt : null)
            .ThenByDescending(item => item.pin)
            .ThenByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToListAsync(ct);
        var companies = await _context.ManufacturingCompanies.AsNoTracking()
            .Where(company => accessibleIds == null || accessibleIds.Contains(company.Id))
            .OrderBy(company => company.Name)
            .Select(company => new { company.Id, company.Name, imageUrl = company.ImageUrl ?? string.Empty })
            .ToListAsync(ct);
        var products = items.Select(item => item.ProductName).Distinct().OrderBy(name => name).ToList();
        return Ok(new
        {
            manufacturingCompanyId,
            productName,
            manufacturingCompanies = companies,
            products,
            items = items.Select(item => new
            {
                item.Id,
                imageUrl = FirstMediaUrl(item.ImageUrl),
                mediaUrls = ParseMediaUrls(item.ImageUrl),
                item.ProductName,
                item.ManufacturingCompanyId,
                item.manufacturingCompanyName,
                item.manufacturingCompanyImageUrl,
                createdAtIso = item.CreatedAt.ToString("o"),
                isPinned = item.pin.HasValue,
                pinnedAtIso = item.pin?.ToString("o") ?? string.Empty,
                item.CopyCount,
                lastCopiedAtIso = item.LastCopiedAt?.ToString("o") ?? string.Empty,
            }),
        });
    }

    [HttpPost("DeleteProductImageAjax")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteProductImageAjax([FromForm] int id, CancellationToken ct)
    {
        var image = await _context.ProductImages.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (image is null) return Ok(new { success = false, message = "المنتج غير موجود." });
        var mediaUrls = ParseMediaUrls(image.ImageUrl);
        _context.ProductImages.Remove(image);
        await _context.SaveChangesAsync(ct);
        foreach (var mediaUrl in mediaUrls)
        {
            var key = TryGetManagedS3Key(mediaUrl);
            if (key is not null)
            {
                try { await _storage.DeleteAsync(key, ct); }
                catch { /* DB deletion succeeded; orphan cleanup can be retried separately. */ }
            }
        }
        return Ok(new { success = true, message = "تم حذف المنتج." });
    }

    [HttpPost("UpdateProductImageAjax")]
    [RequestSizeLimit(50_000_000)]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> UpdateProductImageAjax([FromForm] ProductImageInputForm input, CancellationToken ct)
    {
        if (input.Id <= 0)
            return Ok(new { success = false, message = "تعذر تحديد المنتج المطلوب تعديله." });
        var image = await _context.ProductImages.FirstOrDefaultAsync(item => item.Id == input.Id, ct);
        if (image is null) return Ok(new { success = false, message = "المنتج غير موجود." });
        var validation = await ValidateMediaInputAsync(input, [input.ManufacturingCompanyId], mediaRequired: true, ct);
        if (validation is not null) return Ok(new { success = false, message = validation });
        var oldUrls = ParseMediaUrls(image.ImageUrl);
        var newValue = await SaveMediaInputAsync(input.ProductImageBase64, ct);
        var newUrls = ParseMediaUrls(newValue);
        image.ImageUrl = newValue;
        image.ProductName = input.ProductName.Trim();
        image.ManufacturingCompanyId = input.ManufacturingCompanyId;
        await _context.SaveChangesAsync(ct);
        var kept = newUrls.ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var removedUrl in oldUrls.Where(url => !kept.Contains(url)))
            await DeleteManagedMediaAsync(removedUrl, ct);
        return Ok(new
        {
            success = true,
            message = "تم تعديل المنتج بنجاح",
            item = new
            {
                image.Id,
                imageUrl = FirstMediaUrl(image.ImageUrl),
                mediaUrls = newUrls,
                image.ProductName,
                image.ManufacturingCompanyId,
            },
        });
    }

    [HttpPost("TogglePinProductImageAjax")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,CallCenter")]
    public async Task<IActionResult> TogglePinProductImageAjax([FromForm] int id, CancellationToken ct)
    {
        var userId = User.GetUserId()!;
        var image = await _context.ProductImages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (image is null) return Ok(new { success = false, message = "المنتج غير موجود." });
        if (!await CanAccessCompanyAsync(image.ManufacturingCompanyId, ct))
            return Ok(new { success = false, message = "ليس لديك صلاحية على هذا المتجر." });
        var pin = await _context.ProductImageUserPins
            .FirstOrDefaultAsync(item => item.ProductImageId == id && item.ApplicationUserId == userId, ct);
        DateTime? pinnedAt = null;
        var isPinned = pin is null;
        if (pin is not null) _context.ProductImageUserPins.Remove(pin);
        else
        {
            if (await _context.ProductImageUserPins.CountAsync(item => item.ApplicationUserId == userId, ct) >= 6)
                return Ok(new { success = false, message = "لا يمكن تثبيت أكثر من 6 كروت. ألغِ تثبيت كارت أولًا." });
            pinnedAt = IstanbulTimeHelper.Now;
            _context.ProductImageUserPins.Add(new ProductImageUserPin
            {
                ProductImageId = id,
                ApplicationUserId = userId,
                PinnedAt = pinnedAt.Value,
                PinnedByName = User.Identity?.Name,
            });
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new
        {
            success = true,
            message = isPinned ? "تم تثبيت الصورة " : "تم إلغاء التثبيت",
            isPinned,
            pinnedAtIso = pinnedAt?.ToString("o") ?? string.Empty,
        });
    }

    [HttpPost("TrackProductImageCopyAjax")]
    public async Task<IActionResult> TrackProductImageCopyAjax([FromForm] int id, CancellationToken ct)
    {
        var image = await _context.ProductImages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (image is null) return Ok(new { success = false, message = "المنتج غير موجود." });
        if (!await CanAccessCompanyAsync(image.ManufacturingCompanyId, ct))
            return Ok(new { success = false, message = "ليس لديك صلاحية على هذا المتجر." });
        var now = IstanbulTimeHelper.Now;
        await _context.ProductImages.Where(item => item.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.CopyCount, item => item.CopyCount + 1)
                .SetProperty(item => item.LastCopiedAt, now), ct);
        var copyCount = image.CopyCount + 1;
        return Ok(new { success = true, copyCount, lastCopiedAtIso = now.ToString("o"), isPopular = copyCount >= 3 });
    }

    private async Task<List<int>?> GetAccessibleCompanyIdsAsync(CancellationToken ct)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector")) return null;
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return [];
        return await _context.EmployeeManufacturingCompanies.AsNoTracking()
            .Where(item => item.ApplicationUserId == userId && item.CanSeeManufacturingCompany)
            .Select(item => item.ManufacturingCompanyId)
            .Distinct()
            .ToListAsync(ct);
    }

    private async Task<List<ProductImageDraftItemResponse>> GetDraftItemsAsync(CancellationToken ct) => await _context.ProductImageDrafts
        .AsNoTracking()
        .Where(item => item.CreatedByUserId == User.GetUserId())
        .OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id)
        .Select(item => new ProductImageDraftItemResponse(
            item.Id,
            item.ImageUrl,
            item.ProductName,
            item.ManufacturingCompanyId,
            item.ManufacturingCompany != null ? item.ManufacturingCompany.Name : string.Empty,
            item.ManufacturingCompany != null ? item.ManufacturingCompany.ImageUrl ?? string.Empty : string.Empty,
            item.CreatedAt))
        .ToListAsync(ct);

    private async Task<List<ProductImageCompanyResponse>> GetCompanyOptionsAsync(CancellationToken ct)
    {
        var accessibleIds = await GetAccessibleCompanyIdsAsync(ct);
        return await _context.ManufacturingCompanies.AsNoTracking()
            .Where(item => accessibleIds == null || accessibleIds.Contains(item.Id))
            .OrderBy(item => item.Name)
            .Select(item => new ProductImageCompanyResponse(item.Id, item.Name, item.ImageUrl ?? string.Empty))
            .ToListAsync(ct);
    }

    private async Task<string?> ValidateMediaInputAsync(
        ProductImageInputForm input,
        List<int> companyIds,
        bool mediaRequired,
        CancellationToken ct)
    {
        if (mediaRequired && ParseMediaUrls(input.ProductImageBase64).Count == 0) return "الصورة أو الفيديو مطلوب.";
        if (string.IsNullOrWhiteSpace(input.ProductName)) return "اكتب اسم المنتج.";
        if (companyIds.Count == 0) return "اختر المتجر.";
        var validIds = await _context.ManufacturingCompanies.AsNoTracking()
            .Where(item => companyIds.Contains(item.Id)).Select(item => item.Id).ToListAsync(ct);
        if (validIds.Count != companyIds.Count) return "اختر المتجر.";
        foreach (var companyId in companyIds)
            if (!await CanAccessCompanyAsync(companyId, ct)) return "ليس لديك صلاحية على أحد المتاجر المختارة.";
        return null;
    }

    private async Task<string> SaveMediaInputAsync(string rawValue, CancellationToken ct)
    {
        var output = new List<string>();
        foreach (var value in ParseMediaUrls(rawValue))
        {
            if (!value.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                output.Add(value);
                continue;
            }
            var comma = value.IndexOf(',', StringComparison.Ordinal);
            if (comma < 0) throw new BadRequestException("صيغة الملف غير صحيحة");
            var header = value[..comma];
            var contentType = header[5..].Split(';', 2)[0].ToLowerInvariant();
            var extension = contentType switch
            {
                "image/png" => ".png",
                "image/jpeg" or "image/jpg" => ".jpg",
                "image/webp" => ".webp",
                "image/gif" => ".gif",
                "video/quicktime" => ".mov",
                _ when contentType.StartsWith("video/", StringComparison.Ordinal) => "." + contentType[6..],
                _ => throw new BadRequestException("نوع الملف غير مدعوم. المسموح صورة أو فيديو فقط."),
            };
            byte[] bytes;
            try { bytes = Convert.FromBase64String(value[(comma + 1)..]); }
            catch (FormatException) { throw new BadRequestException("صيغة الملف غير صحيحة"); }
            if (bytes.LongLength > 150_000_000) throw new BadRequestException("حجم الملف أكبر من الحد المسموح.");
            await using var stream = new MemoryStream(bytes, writable: false);
            var stored = await _storage.UploadStreamAsync(
                stream, bytes.LongLength, "productimages", Guid.NewGuid().ToString("N") + extension,
                contentType, User.GetUserId(), ct);
            output.Add($"/Media/File?key={Uri.EscapeDataString(stored.S3Key)}");
        }
        return output.Count == 1 ? output[0] : JsonSerializer.Serialize(output);
    }

    private async Task DeleteManagedMediaAsync(string? value, CancellationToken ct)
    {
        foreach (var mediaUrl in ParseMediaUrls(value))
        {
            var key = TryGetManagedS3Key(mediaUrl);
            if (key is null) continue;
            try { await _storage.DeleteAsync(key, ct); }
            catch { /* Media cleanup must not roll back an already committed row deletion. */ }
        }
    }

    private async Task<bool> CanAccessCompanyAsync(int companyId, CancellationToken ct)
    {
        if (User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector")) return true;
        var userId = User.GetUserId();
        return !string.IsNullOrWhiteSpace(userId) && await _context.EmployeeManufacturingCompanies.AsNoTracking()
            .AnyAsync(item => item.ApplicationUserId == userId && item.ManufacturingCompanyId == companyId && item.CanSeeManufacturingCompany, ct);
    }

    private static List<string> ParseMediaUrls(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return [];
        var value = raw.Trim();
        if (value[0] == '[')
        {
            try { return JsonSerializer.Deserialize<List<string>>(value)?.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() ?? []; }
            catch (JsonException) { }
        }
        return value.Split("||", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    }

    private static string FirstMediaUrl(string? value) => ParseMediaUrls(value).FirstOrDefault() ?? string.Empty;

    private string? TryGetManagedS3Key(string value)
    {
        if (value.StartsWith("/Media/File?", StringComparison.OrdinalIgnoreCase))
        {
            var query = value[(value.IndexOf('?', StringComparison.Ordinal) + 1)..];
            var keyPair = query.Split('&', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault(item => item.StartsWith("key=", StringComparison.OrdinalIgnoreCase));
            return keyPair is null ? null : Uri.UnescapeDataString(keyPair[4..]);
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return null;
        var expectedHost = $"{_storage.BucketName}.s3.{_storage.Region}.amazonaws.com";
        return string.Equals(uri.Host, expectedHost, StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'))
            : null;
    }
}

public record AddProductImageRequest(string ProductName, int ManufacturingCompanyId, string ImageUrl);

public sealed class ProductImageCreateForm
{
    public ProductImageInputForm Input { get; set; } = new();
}

public sealed class ProductImageInputForm
{
    public int Id { get; set; }
    public string ProductImageBase64 { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int ManufacturingCompanyId { get; set; }
}

public sealed record ProductImageDraftItemResponse(
    int Id,
    string ImageUrl,
    string ProductName,
    int ManufacturingCompanyId,
    string ManufacturingCompanyName,
    string ManufacturingCompanyImageUrl,
    DateTime CreatedAt);

public sealed record ProductImageCompanyResponse(int Id, string Name, string ImageUrl);
