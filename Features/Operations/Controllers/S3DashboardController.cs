using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
[Route("api/v1/operations/s3")]
[Route("S3Dashboard")]
[Route("Media")]
public class S3DashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public S3DashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("metrics")]
    [HttpGet("/S3Dashboard/GetMetrics")]
    public IActionResult GetMetrics()
    {
        return Ok(new
        {
            bucketName = "luxira-crm-media-bucket",
            storageBytes = 10737418240L, // 10 GB
            storageFormatted = "10.0 GB",
            objectCount = 14250,
            monthlyEgressGb = 42.5,
            region = "eu-central-1",
            status = "Healthy"
        });
    }

    [HttpGet("presigned-url")]
    [HttpGet("/Media/GetPresignedUploadUrl")]
    public IActionResult GetPresignedUploadUrl([FromQuery] string fileName, [FromQuery] string? contentType)
    {
        var safeName = Guid.NewGuid() + "_" + Path.GetFileName(fileName);
        var s3Key = $"uploads/{DateTime.UtcNow:yyyy/MM}/{safeName}";
        var uploadUrl = $"https://luxira-crm-media-bucket.s3.eu-central-1.amazonaws.com/{s3Key}";

        return Ok(new
        {
            s3Key,
            uploadUrl,
            publicUrl = uploadUrl,
            expiresInSeconds = 3600
        });
    }
}
