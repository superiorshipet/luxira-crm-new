using System.Security.Claims;
using Luxira.Api.Features.Media.DTOs;
using Luxira.Api.Features.Media.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Media.Controllers;

[ApiController]
[Route("api/v1/media")]
[Route("api/[controller]")]
public class MediaController : ControllerBase
{
    private readonly MediaService _service;

    public MediaController(MediaService service)
    {
        _service = service;
    }

    [HttpGet("{*s3Key}")]
    [HttpGet("/Media/GetByKey")]
    public async Task<ActionResult<MediaObjectDto>> GetByKey([FromRoute] string s3Key, CancellationToken ct)
    {
        var result = await _service.GetMediaByKeyAsync(s3Key, ct);
        return Ok(result);
    }
}
