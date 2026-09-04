using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.Media.DTOs;
using Luxira.Api.Features.Media.Models;
using Luxira.Api.Features.Media.Services;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Media.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/media")]
[Route("Media")]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly MediaService _service;
    private readonly S3StorageService _s3;
    private readonly ApplicationDbContext _db;

    public MediaController(MediaService service, S3StorageService s3, ApplicationDbContext db)
    {
        _service = service;
        _s3 = s3;
        _db = db;
    }

    [HttpGet("{*s3Key}")]
    [HttpGet("/Media/GetByKey")]
    public async Task<ActionResult<MediaObjectDto>> GetByKey([RouteOrRequest] string? s3Key, [FromQuery] string? key, CancellationToken ct)
    {
        var targetKey = !string.IsNullOrWhiteSpace(s3Key) ? s3Key : (key ?? string.Empty);
        var result = await _service.GetMediaByKeyAsync(targetKey, ct);
        return Ok(result);
    }

    [HttpGet("/Media/File")]
    [HttpGet("file")]
    public async Task<IActionResult> ServeFile([FromQuery] string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Contains("..", StringComparison.Ordinal) || key.Length > 450)
            return BadRequest();

        key = key.Trim();
        var slash = key.IndexOf('/');
        if (slash <= 0 || slash == key.Length - 1) return BadRequest();

        var prefix = key[..slash];
        if (MediaModuleRegistry.RestrictedPrefixes.TryGetValue(prefix, out var roles) && !roles.Any(User.IsInRole))
            return Forbid();

        if (!MediaModuleRegistry.AllPrefixes.Contains(prefix) &&
            !await _db.S3StoredObjects.AsNoTracking().AnyAsync(item => item.Key == key && !item.IsDeleted, ct))
            return BadRequest();

        Response.Headers.CacheControl = "no-store, max-age=0";
        var presignedUrl = _s3.GetPresignedUrl(key, 30);
        return Redirect(presignedUrl);
    }

    [HttpPost("upload")]
    [HttpPost("/Media/Upload")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> Upload(
        IFormFile file,
        [FromQuery] string? prefix = "uploads",
        CancellationToken ct = default)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided.");

        var userId = User.GetUserId() ?? "system";
        var result = await _s3.UploadAsync(file, prefix ?? "uploads", userId, ct);

        return Ok(new
        {
            result.Id,
            result.S3Key,
            result.BucketName,
            result.PublicUrl,
            result.SizeBytes,
            result.ContentType
        });
    }

    [HttpGet("presigned-upload-url")]
    [HttpGet("/Media/GetPresignedUploadUrl")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public IActionResult GetPresignedUploadUrl(
        [FromQuery] string fileName,
        [FromQuery] string? contentType,
        [FromQuery] string? prefix = "uploads")
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return BadRequest("File name is required.");

        var safeFileName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeFileName)) return BadRequest("File name is required.");
        var safePrefix = MediaModuleRegistry.PrefixForFolder(prefix ?? "uploads");
        var key = $"{safePrefix}/{Guid.NewGuid():N}/{safeFileName}";
        var mime = contentType ?? "application/octet-stream";
        var uploadUrl = _s3.GetPresignedUploadUrl(key, mime, 30);

        return Ok(new
        {
            key,
            uploadUrl,
            contentType = mime,
            expiresInMinutes = 30
        });
    }

    [HttpDelete]
    [HttpDelete("/Media/Delete")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> Delete([FromQuery] string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key))
            return BadRequest("Key is required.");

        await _s3.DeleteAsync(key, ct);
        return Ok(new { success = true, deletedKey = key });
    }
}
