using Luxira.Api.Features.SearchKeywords.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.SearchKeywords.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/search/image")]
[Route("ImageSearch")]
public sealed class ImageSearchController : ControllerBase
{
    private const long MaximumImageBytes = 10L * 1024L * 1024L;
    private readonly ImageSearchService _search;
    private readonly ImageVisionService _vision;
    private readonly ILogger<ImageSearchController> _logger;

    public ImageSearchController(ImageSearchService search, ImageVisionService vision, ILogger<ImageSearchController> logger)
    {
        _search = search;
        _vision = vision;
        _logger = logger;
    }

    [HttpPost("Search")]
    [RequestSizeLimit(MaximumImageBytes + 64 * 1024)]
    public async Task<IActionResult> Search([FromForm] IFormFile? image, CancellationToken ct)
    {
        image ??= Request.HasFormContentType ? Request.Form.Files["file"] : null;
        return image is null
            ? BadRequest(new { success = false, message = "ملف الصورة مطلوب." })
            : await SearchCoreAsync(image, ct);
    }

    // Preserve the new JSON route without ever returning unrelated catalogue rows.
    [HttpPost]
    [HttpPost("SearchByImage")]
    public async Task<IActionResult> SearchByImage([FromBody] ImageSearchRequest request, CancellationToken ct)
    {
        if (!TryReadDataUrl(request.ImageUrl, out var bytes, out var contentType) || bytes.Length > MaximumImageBytes)
            return BadRequest(new { success = false, message = "ImageUrl must be an image data URL no larger than 10 MB." });
        await using var stream = new MemoryStream(bytes, writable: false);
        var file = new FormFile(stream, 0, bytes.Length, "image", "search-image") { ContentType = contentType };
        return await SearchCoreAsync(file, ct);
    }

    private async Task<IActionResult> SearchCoreAsync(IFormFile image, CancellationToken ct)
    {
        if (image.Length == 0) return BadRequest(new { success = false, message = "ملف الصورة مطلوب." });
        if (image.Length > MaximumImageBytes)
            return BadRequest(new { success = false, message = "حجم الصورة يجب ألا يتجاوز 10 ميجابايت." });
        if (!image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { success = false, message = "نوع الملف غير مدعوم، يرجى رفع صورة." });

        try
        {
            var hash = await _search.ComputeHashAsync(image, ct);
            if (!hash.HasValue)
                return BadRequest(new { success = false, message = "ملف الصورة غير صالح أو غير مدعوم." });

            var productId = await _search.FindProductAsync(hash.Value, ct);
            if (productId.HasValue)
                return Ok(new { success = true, matchType = "product", productId = productId.Value, message = "تم العثور على المنتج وجاري عرض كل طلباته." });

            var order = await _search.FindOrderAsync(hash.Value, ct);
            if (order is not null)
                return Ok(new
                {
                    success = true,
                    matchType = "order",
                    query = order.ExternalOrderId.HasValue ? $"*{order.ExternalOrderId}" : order.Id.ToString(),
                    text = order.Id.ToString(),
                    orderId = order.Id
                });

            var vision = await _vision.ExtractAsync(image, ct);
            if (vision.Error is not null)
                return StatusCode(StatusCodes.Status502BadGateway, new { success = false, needsOcr = true, message = vision.Error });
            if (!string.IsNullOrWhiteSpace(vision.Query))
                return Ok(new { success = true, matchType = "ocr", query = vision.Query, needsOcr = false, message = "تم استخراج نص البحث من الصورة." });
            return Ok(new { success = false, needsOcr = true, query = (string?)null, message = "لم توجد صورة أوردر مطابقة؛ جاري قراءة النص من الصورة." });
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Image OCR provider request failed");
            return StatusCode(StatusCodes.Status502BadGateway,
                new { success = false, needsOcr = true, message = "Image OCR provider is unavailable." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image search failed");
            return BadRequest(new { success = false, message = "حدث خطأ أثناء البحث." });
        }
    }

    private static bool TryReadDataUrl(string value, out byte[] bytes, out string contentType)
    {
        bytes = [];
        contentType = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !value.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase)) return false;
        var delimiter = value.IndexOf(',');
        var semicolon = value.IndexOf(';');
        if (delimiter < 0 || semicolon < 0 || !value[..delimiter].EndsWith(";base64", StringComparison.OrdinalIgnoreCase)) return false;
        contentType = value[5..semicolon];
        try
        {
            bytes = Convert.FromBase64String(value[(delimiter + 1)..]);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}

public sealed record ImageSearchRequest(string ImageUrl, string? FeatureVector);
