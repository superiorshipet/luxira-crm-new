using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector")]
[Route("api/v1/marketing/campaigns")]
[Route("Campaign")]
public class CampaignController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public CampaignController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetCampaigns")]
    public async Task<ActionResult<List<AdvertisingCampaign>>> GetCampaigns(CancellationToken ct)
    {
        var list = await _context.Set<AdvertisingCampaign>()
            .AsNoTracking()
            .OrderByDescending(campaign => campaign.CreatedAt)
            .ToListAsync(ct);
        return Ok(list);
    }
}
