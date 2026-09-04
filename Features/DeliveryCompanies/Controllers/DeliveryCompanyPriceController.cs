using Luxira.Api.Data;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/delivery-companies/prices")]
[Route("DeliveryCompanyPrice")]
public class DeliveryCompanyPriceController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DeliveryCompanyPriceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/DeliveryCompanyPrice/Index")]
    [HttpPost("/DeliveryCompanyPrice/Index")]
    [Authorize(Roles = "Admin,Administrator,DeliveryCompany,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> Index([FromQuery] int? deliveryCompanyId, [FromQuery] int? country, CancellationToken ct = default)
    {
        var query = _context.DeliveryCompanyPrices
            .Include(p => p.DeliveryCompany)
            .Where(p => p.DeliveryCompany != null && !p.DeliveryCompany.IsRepresentative)
            .AsNoTracking()
            .AsQueryable();

        if (deliveryCompanyId.HasValue) query = query.Where(p => p.DeliveryCompanyId == deliveryCompanyId.Value);
        if (country.HasValue) query = query.Where(p => p.Country == country.Value);

        var prices = await query.Select(item => new { item.Id, item.DeliveryCompanyId, item.Country, item.City, item.Price, deliveryCompanyName = item.DeliveryCompany!.Name }).ToListAsync(ct);
        return Ok(prices);
    }

    [HttpGet("/DeliveryCompanyPrice/Create")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> CreateForm(CancellationToken ct) => Ok(new
    {
        deliveryCompanies = await _context.DeliveryCompanies.AsNoTracking().Where(item => !item.IsRepresentative && item.IsShown).OrderBy(item => item.Name).Select(item => new { item.Id, item.Name, item.Country }).ToListAsync(ct)
    });

    [HttpGet("/DeliveryCompanyPrice/Edit")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> EditForm([FromQuery] int id, CancellationToken ct)
    {
        var price = await _context.DeliveryCompanyPrices.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return price is null ? NotFound() : Ok(price);
    }

    [HttpPost("Create")]
    [HttpPost("/DeliveryCompanyPrice/Create")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryPriceItemRequest request, CancellationToken ct = default)
    {
        var price = new DeliveryCompanyPrice
        {
            DeliveryCompanyId = request.DeliveryCompanyId,
            Country = request.Country,
            City = request.City,
            Price = request.Price
        };

        await _context.DeliveryCompanyPrices.AddAsync(price, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(price);
    }

    [HttpPost("Edit/{id:int}")]
    [HttpPut("{id:int}")]
    [HttpPost("/DeliveryCompanyPrice/Edit")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> Edit([RouteOrRequest] int id, [FromBody] CreateDeliveryPriceItemRequest request, CancellationToken ct = default)
    {
        var price = await _context.DeliveryCompanyPrices.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (price == null) return NotFound("Delivery price entry not found.");

        price.Price = request.Price;
        price.City = request.City ?? price.City;
        price.Country = request.Country;

        await _context.SaveChangesAsync(ct);
        return Ok(price);
    }

    [HttpGet("GetAvailableCities")]
    [HttpGet("/DeliveryCompanyPrice/GetAvailableCities")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> GetAvailableCities([FromQuery] int deliveryCompanyId, [FromQuery] string country, CancellationToken ct = default)
    {
        var cities = await _context.CamexCities.AsNoTracking().Select(c => c.CityName).Distinct().ToListAsync(ct);
        return Ok(cities);
    }
}

[ApiController]
[Authorize]
[Route("api/v1/delivery-representatives/prices")]
[Route("DeliveryRepresentativePrice")]
public class DeliveryRepresentativePriceController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public DeliveryRepresentativePriceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/DeliveryRepresentativePrice/Index")]
    [HttpPost("/DeliveryRepresentativePrice/Index")]
    [Authorize(Roles = "Admin,Administrator,DeliveryRepresentative,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> Index([FromQuery] int? deliveryRepresentativeId, [FromQuery] int? country, CancellationToken ct = default)
    {
        var query = _context.DeliveryCompanyPrices
            .Include(p => p.DeliveryCompany)
            .Where(p => p.DeliveryCompany != null && p.DeliveryCompany.IsRepresentative)
            .AsNoTracking()
            .AsQueryable();

        if (deliveryRepresentativeId.HasValue) query = query.Where(p => p.DeliveryCompanyId == deliveryRepresentativeId.Value);
        if (country.HasValue) query = query.Where(p => p.Country == country.Value);

        var prices = await query.Select(item => new { item.Id, item.DeliveryCompanyId, item.Country, item.City, item.Price, deliveryRepresentativeName = item.DeliveryCompany!.Name }).ToListAsync(ct);
        return Ok(prices);
    }

    [HttpGet("/DeliveryRepresentativePrice/Create")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> CreateForm(CancellationToken ct) => Ok(new
    {
        representatives = await _context.DeliveryCompanies.AsNoTracking().Where(item => item.IsRepresentative && item.IsShown).OrderBy(item => item.Name).Select(item => new { item.Id, item.Name, item.Country }).ToListAsync(ct)
    });

    [HttpPost("Create")]
    [HttpPost("/DeliveryRepresentativePrice/Create")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryPriceItemRequest request, CancellationToken ct = default)
    {
        var price = new DeliveryCompanyPrice
        {
            DeliveryCompanyId = request.DeliveryCompanyId,
            Country = request.Country,
            City = request.City,
            Price = request.Price
        };

        await _context.DeliveryCompanyPrices.AddAsync(price, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(price);
    }

    [HttpGet("/DeliveryRepresentativePrice/Edit")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> EditForm([FromQuery] int id, CancellationToken ct)
    {
        var price = await _context.DeliveryCompanyPrices.AsNoTracking().Include(item => item.DeliveryCompany)
            .FirstOrDefaultAsync(item => item.Id == id && item.DeliveryCompany != null && item.DeliveryCompany.IsRepresentative, ct);
        return price is null ? NotFound() : Ok(new { price.Id, price.DeliveryCompanyId, price.Country, price.City, price.Price, deliveryRepresentativeName = price.DeliveryCompany!.Name });
    }

    [HttpPost("/DeliveryRepresentativePrice/Edit")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> Edit([FromQuery] int id, [FromBody] CreateDeliveryPriceItemRequest request, CancellationToken ct)
    {
        var price = await _context.DeliveryCompanyPrices.Include(item => item.DeliveryCompany)
            .FirstOrDefaultAsync(item => item.Id == id && item.DeliveryCompany != null && item.DeliveryCompany.IsRepresentative, ct);
        if (price is null) return NotFound();
        price.Price = request.Price;
        price.Country = request.Country;
        price.City = request.City;
        price.DeliveryCompanyId = request.DeliveryCompanyId;
        await _context.SaveChangesAsync(ct);
        return Ok(price);
    }

    [HttpGet("/DeliveryRepresentativePrice/GetAvailableDeliveryRepresentatives")]
    [Authorize(Roles = "Admin,Administrator,FollowUpDepartment,ExecutiveDirector")]
    public async Task<IActionResult> GetAvailableDeliveryRepresentatives([FromQuery] int country, [FromQuery] string? city, CancellationToken ct)
    {
        var assigned = _context.DeliveryCompanyPrices.AsNoTracking().Where(price => price.Country == country && price.City == city).Select(price => price.DeliveryCompanyId);
        return Ok(await _context.DeliveryCompanies.AsNoTracking()
            .Where(item => item.IsRepresentative && item.IsShown && item.Country == country && !assigned.Contains(item.Id))
            .OrderBy(item => item.Name).Select(item => new { item.Id, item.Name }).ToListAsync(ct));
    }
}

public record CreateDeliveryPriceItemRequest(int DeliveryCompanyId, int Country, string? City, decimal Price);
