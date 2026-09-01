using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/marketing/scripts")]
[Route("StoreScript")]
public class StoreScriptController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public StoreScriptController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetScripts")]
    public async Task<ActionResult<List<StoreScript>>> GetScripts(
        [FromQuery] int? manufacturingCompanyId,
        [FromQuery] string? platform,
        CancellationToken ct)
    {
        var query = _context.Set<StoreScript>()
            .AsNoTracking()
            .Where(script => !script.IsDeleted)
            .AsQueryable();

        if (manufacturingCompanyId.HasValue && manufacturingCompanyId.Value > 0)
        {
            query = query.Where(script => script.ManufacturingCompanyId == manufacturingCompanyId.Value);
        }

        if (!string.IsNullOrWhiteSpace(platform))
        {
            query = query.Where(script => script.Platform == platform);
        }

        var list = await query
            .OrderByDescending(script => script.UpdatedAt)
            .Take(200)
            .ToListAsync(ct);
        return Ok(list);
    }
}
