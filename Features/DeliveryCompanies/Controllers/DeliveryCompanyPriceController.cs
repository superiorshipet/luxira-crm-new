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
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
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
    public async Task<IActionResult> Index([FromQuery] int? deliveryCompanyId, [FromQuery] int? country, CancellationToken ct = default)
    {
        var query = _context.DeliveryCompanyPrices
            .Include(p => p.DeliveryCompany)
            .Where(p => p.DeliveryCompany != null && !p.DeliveryCompany.IsRepresentative)
            .AsNoTracking()
            .AsQueryable();

        if (deliveryCompanyId.HasValue) query = query.Where(p => p.DeliveryCompanyId == deliveryCompanyId.Value);
        if (country.HasValue) query = query.Where(p => p.Country == country.Value);

        var prices = await query.ToListAsync(ct);
        return Ok(prices);
    }

    [HttpPost("Create")]
    [HttpPost("/DeliveryCompanyPrice/Create")]
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
    public async Task<IActionResult> Edit([FromRoute] int id, [FromBody] CreateDeliveryPriceItemRequest request, CancellationToken ct = default)
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
    public async Task<IActionResult> GetAvailableCities([FromQuery] int deliveryCompanyId, [FromQuery] string country, CancellationToken ct = default)
    {
        var cities = await _context.CamexCities.AsNoTracking().Select(c => c.CityName).Distinct().ToListAsync(ct);
        return Ok(cities);
    }
}

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
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
    public async Task<IActionResult> Index([FromQuery] int? deliveryRepresentativeId, [FromQuery] int? country, CancellationToken ct = default)
    {
        var query = _context.DeliveryCompanyPrices
            .Include(p => p.DeliveryCompany)
            .Where(p => p.DeliveryCompany != null && p.DeliveryCompany.IsRepresentative)
            .AsNoTracking()
            .AsQueryable();

        if (deliveryRepresentativeId.HasValue) query = query.Where(p => p.DeliveryCompanyId == deliveryRepresentativeId.Value);
        if (country.HasValue) query = query.Where(p => p.Country == country.Value);

        var prices = await query.ToListAsync(ct);
        return Ok(prices);
    }

    [HttpPost("Create")]
    [HttpPost("/DeliveryRepresentativePrice/Create")]
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
}

public record CreateDeliveryPriceItemRequest(int DeliveryCompanyId, int Country, string? City, decimal Price);
