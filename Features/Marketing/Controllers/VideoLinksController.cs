using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Marketing")]
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

        var links = await query.OrderByDescending(v => v.CreatedAt).ToListAsync(ct);
        return Ok(links);
    }

    [HttpPost("Create")]
    [HttpPost("/VideoLinks/Create")]
    public async Task<IActionResult> Create([FromBody] VideoLinkUpsertRequest request, CancellationToken ct = default)
    {
        var link = new VideoLink
        {
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            Url = request.Url,
            CreatedAt = IstanbulTimeHelper.Now,
            CreatedByUserId = User.GetUserId(),
            CreatedByName = User.Identity?.Name
        };

        await _context.VideoLinks.AddAsync(link, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(link);
    }

    [HttpPost("Edit")]
    [HttpPost("/VideoLinks/Edit")]
    public async Task<IActionResult> Edit([FromBody] VideoLinkUpsertRequest request, CancellationToken ct = default)
    {
        var link = await _context.VideoLinks.FirstOrDefaultAsync(v => v.Id == request.Id, ct);
        if (link == null) return NotFound("Video link not found.");

        link.ManufacturingCompanyId = request.ManufacturingCompanyId;
        link.Url = request.Url;
        link.UpdatedAt = IstanbulTimeHelper.Now;
        link.UpdatedByUserId = User.GetUserId();
        link.UpdatedByName = User.Identity?.Name;

        await _context.SaveChangesAsync(ct);
        return Ok(link);
    }

    [HttpPost("Delete/{id:int}")]
    [HttpDelete("{id:int}")]
    [HttpPost("/VideoLinks/Delete")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct = default)
    {
        var link = await _context.VideoLinks.FirstOrDefaultAsync(v => v.Id == id, ct);
        if (link == null) return NotFound("Video link not found.");

        link.IsDeleted = true;
        link.DeletedAt = IstanbulTimeHelper.Now;
        link.DeletedByUserId = User.GetUserId();
        link.DeletedByName = User.Identity?.Name;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

public sealed record VideoLinkUpsertRequest(int? Id, int ManufacturingCompanyId, string Url);
