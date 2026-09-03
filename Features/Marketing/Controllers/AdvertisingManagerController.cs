using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Luxira.Api.Features.Orders.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector")]
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
            query = query.Where(campaign => campaign.Country == country.Value);
        }

        var list = await query.OrderByDescending(campaign => campaign.CreatedAt).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<AdvertisingCampaign>> CreateCampaign([FromBody] CreateAdCampaignRequest request, CancellationToken ct)
    {
        var c = new AdvertisingCampaign
        {
            Name = request.Name,
            Country = request.Country,
            MainWarehouseId = request.MainWarehouseId,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Set<AdvertisingCampaign>().AddAsync(c, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(c);
    }

    [HttpGet("roi")]
    [HttpGet("GetCampaignRoi/{campaignId:int}")]
    public async Task<ActionResult<CampaignRoiDto>> GetCampaignRoi([RouteOrRequest] int campaignId, CancellationToken ct)
    {
        var campaign = await _context.Set<AdvertisingCampaign>().FindAsync([campaignId], ct);
        if (campaign == null)
        {
            return NotFound(new { message = $"Campaign {campaignId} not found." });
        }

        var orders = await _context.Orders
            .AsNoTracking()
            .Where(o => o.CampaignId == campaignId)
            .ToListAsync(ct);

        int totalOrders = orders.Count;
        int deliveredOrders = orders.Count(o => o.OrderStatus == OrderStatusCodes.Delivered);
        decimal totalRevenue = orders
            .Where(o => o.OrderStatus == OrderStatusCodes.Delivered)
            .Sum(o => o.TotalPrice);

        return Ok(new CampaignRoiDto(
            campaign.Id,
            campaign.Name ?? string.Empty,
            totalOrders,
            deliveredOrders,
            totalRevenue
        ));
    }
}

public record CreateAdCampaignRequest(
    string Name,
    int Country,
    int? MainWarehouseId,
    int? ManufacturingCompanyId);

public record CampaignRoiDto(
    int CampaignId,
    string CampaignName,
    int TotalOrders,
    int DeliveredOrders,
    decimal Revenue);
