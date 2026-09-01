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
    public async Task<ActionResult<List<StoreScript>>> GetScripts([FromQuery] string? category, CancellationToken ct)
    {
        var query = _context.Set<StoreScript>().AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(s => s.Category == category);
        }

        var list = await query.ToListAsync(ct);
        return Ok(list);
    }
}
