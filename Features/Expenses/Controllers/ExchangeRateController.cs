using Luxira.Api.Data;
using Luxira.Api.Features.Expenses.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/exchange-rates")]
[Route("ExchangeRate")]
public class ExchangeRateController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ExchangeRateController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/ExchangeRate/Index")]
    [Authorize(Roles = "Admin,Administrator,Accountant,Observer,DeliveryCompany,ExecutiveDirector,DeliveryRepresentative")]
    public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = _context.ExchangeRates.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rates = await query
            .OrderBy(r => r.Country)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items = rates });
    }

    [HttpGet("{id:int}")]
    [HttpGet("/ExchangeRate/Edit/{id:int}")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetById([RouteOrRequest] int id, CancellationToken ct)
    {
        var rate = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rate == null) return NotFound("Exchange rate not found.");
        return Ok(rate);
    }

    [HttpPost]
    [HttpPost("Create")]
    [HttpPost("/ExchangeRate/Create")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> Create([FromBody] CreateExchangeRateRequest request, CancellationToken ct)
    {
        var rate = new ExchangeRate
        {
            Country = request.Country,
            BuyToUSD = request.BuyToUSD,
            SellToUSD = request.SellToUSD
        };

        await _context.ExchangeRates.AddAsync(rate, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(rate);
    }

    [HttpPost("{id:int}")]
    [HttpPut("{id:int}")]
    [HttpPost("Edit/{id:int}")]
    [HttpPost("/ExchangeRate/Edit")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> Edit([RouteOrRequest] int id, [FromBody] CreateExchangeRateRequest request, CancellationToken ct)
    {
        var rate = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rate == null) return NotFound("Exchange rate not found.");

        rate.Country = request.Country;
        rate.BuyToUSD = request.BuyToUSD;
        rate.SellToUSD = request.SellToUSD;

        await _context.SaveChangesAsync(ct);
        return Ok(rate);
    }
}

public sealed record CreateExchangeRateRequest(int Country, decimal BuyToUSD, decimal SellToUSD);
