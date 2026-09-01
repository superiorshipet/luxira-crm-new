using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/marketing/advertising")]
[Route("AdvertisingManager")]
public class AdvertisingManagerController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdvertisingManagerController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetCampaigns")]
    public async Task<ActionResult<List<AdvertisingCampaign>>> GetCampaigns([FromQuery] int? country, CancellationToken ct)
    {
        var query = _context.Set<AdvertisingCampaign>().AsNoTracking().AsQueryable();
        if (country.HasValue && country.Value > 0)
        {
            query = query.Where(c => c.TargetCountry == country.Value);
        }

        var list = await query.OrderByDescending(c => c.StartDate).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<AdvertisingCampaign>> CreateCampaign([FromBody] CreateAdCampaignRequest request, CancellationToken ct)
    {
        var c = new AdvertisingCampaign
        {
            Name = request.Name,
            Platform = request.Platform ?? "Facebook",
            Budget = request.Budget,
            Spent = 0,
            TargetCountry = request.TargetCountry,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            Status = "Active",
            StartDate = DateTime.UtcNow
        };

        await _context.Set<AdvertisingCampaign>().AddAsync(c, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(c);
    }
}

public record CreateAdCampaignRequest(string Name, string? Platform, decimal Budget, int TargetCountry, int? ManufacturingCompanyId);
