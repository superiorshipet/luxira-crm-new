using Luxira.Api.Data;
using Luxira.Api.Features.Marketing.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Luxira.Api.Infrastructure.S3;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector")]
[Route("api/v1/marketing/campaigns")]
[Route("Campaign")]
public class CampaignController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _storage;

    public CampaignController(ApplicationDbContext context, S3StorageService storage)
    {
        _context = context;
        _storage = storage;
    }

    [HttpGet]
    [HttpGet("GetCampaigns")]
    [HttpGet("/Campaign/Index")]
    [HttpPost("/Campaign/Index")]
    public async Task<ActionResult<List<AdvertisingCampaign>>> GetCampaigns(CancellationToken ct)
    {
        var list = await _context.Set<AdvertisingCampaign>()
            .AsNoTracking()
            .OrderByDescending(campaign => campaign.CreatedAt)
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("/Campaign/CampaignAnalytics")]
    [HttpPost("/Campaign/CampaignAnalytics")]
    public async Task<IActionResult> CampaignAnalytics(
        [FromQuery] int? countryId,
        [FromQuery] int? campaignId,
        [FromQuery] DateTime? startDay,
        [FromQuery] DateTime? endDay,
        [FromQuery] int? productId,
        CancellationToken ct)
    {
        var range = OperationalRange(startDay, endDay);
        var query = _context.Orders.AsNoTracking().Where(order => order.CampaignId.HasValue && (order.InstantAddedDate ?? order.CreatedDate) >= range.Start && (order.InstantAddedDate ?? order.CreatedDate) < range.End);
        if (countryId.HasValue) query = query.Where(order => _context.AdvertisingCampaigns.Any(campaign => campaign.Id == order.CampaignId && campaign.Country == countryId));
        if (campaignId is > 0) query = query.Where(order => order.CampaignId == campaignId);
        if (productId is > 0) query = query.Where(order => _context.AdvertisingCampaigns.Any(campaign => campaign.Id == order.CampaignId && campaign.MainWarehouseId == productId));
        var total = await query.GroupBy(_ => 1).Select(group => new { totalOrders = group.Count(), totalPrice = group.Sum(order => order.TotalPrice) }).FirstOrDefaultAsync(ct);
        var campaigns = await query.GroupBy(order => order.CampaignId!.Value).Select(group => new
        {
            campaignId = group.Key,
            campaignName = _context.AdvertisingCampaigns.Where(campaign => campaign.Id == group.Key).Select(campaign => campaign.Name).FirstOrDefault(),
            imageUrl = _context.AdvertisingCampaigns.Where(campaign => campaign.Id == group.Key).Select(campaign => campaign.ImageUrl).FirstOrDefault(),
            totalOrders = group.Count(),
            totalSales = group.Sum(order => order.TotalPrice)
        }).OrderByDescending(item => item.totalOrders).ToListAsync(ct);
        return Ok(new { success = true, periodStart = range.Start, periodEnd = range.End, totalOrders = total?.totalOrders ?? 0, totalPriceUSD = total?.totalPrice ?? 0m, campaignSales = campaigns });
    }

    [HttpGet("/Campaign/Create")]
    public async Task<IActionResult> Create(CancellationToken ct) => Ok(new
    {
        stores = await _context.ManufacturingCompanies.AsNoTracking().Where(store => store.IsShown).OrderBy(store => store.Name).Select(store => new { store.Id, store.Name }).ToListAsync(ct),
        products = await _context.MainWarehouses.AsNoTracking().OrderBy(product => product.Name).Select(product => new { product.Id, product.Name }).ToListAsync(ct)
    });

    [HttpPost("/Campaign/Create")]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Create([FromForm] CampaignFormRequest request, CancellationToken ct)
    {
        var countries = (request.SelectedCountries ?? []).Distinct().DefaultIfEmpty(request.Country ?? 0).ToList();
        var files = (request.ImageFiles ?? []).Concat(request.ImageFile is null ? [] : [request.ImageFile]).Where(file => file.Length > 0).ToList();
        var uploads = new List<(string? Url, string? Key)>();
        foreach (var file in files)
        {
            var stored = await _storage.UploadAsync(file, "campaigns", User.Identity?.Name, ct);
            uploads.Add((stored.PublicUrl, stored.S3Key));
        }
        if (uploads.Count == 0) uploads.Add((null, null));
        var productName = await _context.MainWarehouses.AsNoTracking().Where(product => product.Id == request.MainWarehouseId).Select(product => product.Name).FirstOrDefaultAsync(ct);
        var entities = countries.SelectMany(country => uploads.Select(upload => new AdvertisingCampaign
        {
            Country = country,
            MainWarehouseId = request.MainWarehouseId,
            ManufacturingCompanyId = request.ManufacturingCompanyId,
            Name = productName,
            ImageUrl = upload.Url,
            ImageS3Key = upload.Key,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        })).ToList();
        _context.AdvertisingCampaigns.AddRange(entities);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, items = entities });
    }

    [HttpGet("/Campaign/Edit")]
    public async Task<IActionResult> Edit([FromQuery] int id, CancellationToken ct)
    {
        var campaign = await _context.AdvertisingCampaigns.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return campaign is null ? NotFound() : Ok(campaign);
    }

    [HttpPost("/Campaign/Edit")]
    public async Task<IActionResult> Edit([FromQuery] int id, [FromForm] CampaignFormRequest request, CancellationToken ct)
    {
        var campaign = await _context.AdvertisingCampaigns.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (campaign is null) return NotFound();
        if (request.ImageFile is { Length: > 0 })
        {
            var stored = await _storage.UploadAsync(request.ImageFile, "campaigns", User.Identity?.Name, ct);
            campaign.ImageUrl = stored.PublicUrl;
            campaign.ImageS3Key = stored.S3Key;
        }
        var countries = (request.SelectedCountries ?? []).Distinct().DefaultIfEmpty(request.Country ?? 0).ToList();
        campaign.Country = countries[0];
        campaign.MainWarehouseId = request.MainWarehouseId;
        campaign.ManufacturingCompanyId = request.ManufacturingCompanyId;
        campaign.IsActive = request.IsActive;
        campaign.Name = await _context.MainWarehouses.AsNoTracking().Where(product => product.Id == request.MainWarehouseId).Select(product => product.Name).FirstOrDefaultAsync(ct);
        foreach (var country in countries.Skip(1))
            _context.AdvertisingCampaigns.Add(new AdvertisingCampaign { Country = country, MainWarehouseId = campaign.MainWarehouseId, ManufacturingCompanyId = campaign.ManufacturingCompanyId, Name = campaign.Name, ImageUrl = campaign.ImageUrl, ImageS3Key = campaign.ImageS3Key, IsActive = campaign.IsActive, CreatedAt = DateTime.UtcNow });
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, campaign });
    }

    [HttpPost("/Campaign/Delete")]
    public async Task<IActionResult> Delete([FromForm] int campaignId, CancellationToken ct)
    {
        var campaign = await _context.AdvertisingCampaigns.FirstOrDefaultAsync(item => item.Id == campaignId, ct);
        if (campaign is null) return NotFound();
        _context.AdvertisingCampaigns.Remove(campaign);
        await _context.SaveChangesAsync(ct);
        if (!string.IsNullOrWhiteSpace(campaign.ImageS3Key)) await _storage.DeleteAsync(campaign.ImageS3Key, ct);
        return Ok(new { success = true });
    }

    [HttpPost("/Campaign/ToggleStatus")]
    public async Task<IActionResult> ToggleStatus([FromForm] int campaignId, [FromForm] bool isActive, CancellationToken ct)
    {
        var changed = await _context.AdvertisingCampaigns.Where(campaign => campaign.Id == campaignId).ExecuteUpdateAsync(setters => setters.SetProperty(campaign => campaign.IsActive, isActive), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true, isActive });
    }

    [HttpGet("/Campaign/GetActiveCampaigns")]
    [HttpPost("/Campaign/GetActiveCampaigns")]
    public async Task<IActionResult> GetActiveCampaigns(CancellationToken ct) => Ok(await ActiveCampaigns().ToListAsync(ct));

    [HttpGet("/Campaign/GetCampaignsByCountry")]
    [HttpPost("/Campaign/GetCampaignsByCountry")]
    public async Task<IActionResult> GetCampaignsByCountry([FromQuery] int country, CancellationToken ct) => Ok(await ActiveCampaigns().Where(campaign => campaign.Country == country).ToListAsync(ct));

    private IQueryable<CampaignListItem> ActiveCampaigns() => _context.AdvertisingCampaigns.AsNoTracking().Where(campaign => campaign.IsActive).OrderByDescending(campaign => campaign.CreatedAt)
        .Select(campaign => new CampaignListItem(campaign.Id, campaign.ImageUrl, campaign.Country, campaign.MainWarehouseId, campaign.MainWarehouse == null ? null : campaign.MainWarehouse.Name, campaign.IsActive, campaign.ManufacturingCompanyId));

    private static (DateTime Start, DateTime End) OperationalRange(DateTime? from, DateTime? to)
    {
        if (!from.HasValue || !to.HasValue)
        {
            var now = DateTime.Now;
            var day = now.TimeOfDay < TimeSpan.FromHours(10.5) ? now.Date.AddDays(-1) : now.Date;
            return (day.AddHours(10.5), day.AddDays(1).AddHours(10.5));
        }
        var start = from.Value.Date.AddHours(10.5);
        var end = to.Value.Date.AddDays(1).AddHours(10.5);
        return (start, end <= start ? start.AddDays(1) : end);
    }
}

public sealed record CampaignFormRequest(int? Country, List<int>? SelectedCountries, int? MainWarehouseId, int? ManufacturingCompanyId, bool IsActive, IFormFile? ImageFile, List<IFormFile>? ImageFiles);
public sealed record CampaignListItem(int Id, string? ImageUrl, int Country, int? MainWarehouseId, string? ProductName, bool IsActive, int? ManufacturingCompanyId);
