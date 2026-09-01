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
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Accountant")]
[Route("api/v1/expenses/exchange-rates")]
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
    public async Task<IActionResult> Index([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = _context.ExchangeRates.AsNoTracking();
        var total = await query.CountAsync(ct);
        var rates = await query
            .OrderByDescending(r => r.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return Ok(new { total, page, pageSize, items = rates });
    }

    [HttpGet("{id:int}")]
    [HttpGet("/ExchangeRate/Edit/{id:int}")]
    public async Task<IActionResult> GetById([FromRoute] int id, CancellationToken ct)
    {
        var rate = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rate == null) return NotFound("Exchange rate not found.");
        return Ok(rate);
    }

    [HttpPost]
    [HttpPost("Create")]
    [HttpPost("/ExchangeRate/Create")]
    public async Task<IActionResult> Create([FromBody] CreateExchangeRateRequest request, CancellationToken ct)
    {
        var rate = new ExchangeRate
        {
            FromCurrency = request.FromCurrency ?? "USD",
            ToCurrency = request.ToCurrency ?? "TRY",
            Rate = request.Rate,
            UpdatedAt = IstanbulTimeHelper.Now
        };

        await _context.ExchangeRates.AddAsync(rate, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(rate);
    }

    [HttpPost("{id:int}")]
    [HttpPut("{id:int}")]
    [HttpPost("Edit/{id:int}")]
    [HttpPost("/ExchangeRate/Edit")]
    public async Task<IActionResult> Edit([FromRoute] int id, [FromBody] CreateExchangeRateRequest request, CancellationToken ct)
    {
        var rate = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rate == null) return NotFound("Exchange rate not found.");

        rate.FromCurrency = request.FromCurrency ?? rate.FromCurrency;
        rate.ToCurrency = request.ToCurrency ?? rate.ToCurrency;
        rate.Rate = request.Rate;
        rate.UpdatedAt = IstanbulTimeHelper.Now;

        await _context.SaveChangesAsync(ct);
        return Ok(rate);
    }
}

public record CreateExchangeRateRequest(string? FromCurrency, string? ToCurrency, decimal Rate);
