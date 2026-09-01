using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/marketing/leads")]
[Route("Lead")]
public class LeadController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public LeadController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetLeads")]
    public async Task<ActionResult<List<MarketingLead>>> GetLeads([FromQuery] int? country, CancellationToken ct)
    {
        var query = _context.Set<MarketingLead>().AsNoTracking().AsQueryable();
        if (country.HasValue && country.Value > 0)
        {
            query = query.Where(l => l.Country == country.Value);
        }

        var list = await query.OrderByDescending(l => l.CreatedAt).ToListAsync(ct);
        return Ok(list);
    }
}
