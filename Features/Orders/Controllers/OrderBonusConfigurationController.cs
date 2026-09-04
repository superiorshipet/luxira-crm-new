using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator")]
[Route("api/v1/orders/bonus-configurations")]
[Route("OrderBonusConfiguration")]
public class OrderBonusConfigurationController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public OrderBonusConfigurationController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/OrderBonusConfiguration/Index")]
    [HttpPost("/OrderBonusConfiguration/Index")]
    public async Task<IActionResult> Index([FromQuery] int? country, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var query = _context.OrderBonusConfigurations.AsNoTracking().AsQueryable();
        if (country.HasValue) query = query.Where(c => c.Country == country.Value);

        var total = await query.CountAsync(ct);
        var configs = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new { total, page, pageSize, items = configs });
    }

    [HttpGet("/OrderBonusConfiguration/Create")]
    public async Task<IActionResult> CreateForm(CancellationToken ct) => Ok(new
    {
        employees = await _context.Employees.AsNoTracking().Where(item => item.IsActive && item.IsShown).OrderBy(item => item.Name).Select(item => new { item.Id, Name = item.DisplayName ?? item.Name }).ToListAsync(ct)
    });

    [HttpGet("/OrderBonusConfiguration/Edit")]
    public async Task<IActionResult> EditForm([FromQuery] int id, CancellationToken ct)
    {
        var config = await _context.OrderBonusConfigurations.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return config is null ? NotFound() : Ok(config);
    }

    [HttpPost("Create")]
    [HttpPost("/OrderBonusConfiguration/Create")]
    public async Task<IActionResult> Create([FromBody] CreateOrderBonusConfigRequest request, CancellationToken ct = default)
    {
        var config = new OrderBonusConfiguration
        {
            Country = request.Country,
            OrderThreshold = request.OrderThreshold,
            FlatBonusAmount = request.FlatBonusAmount,
            PercentageBonus = request.PercentageBonus,
            EmployeeId = request.EmployeeId
        };

        await _context.OrderBonusConfigurations.AddAsync(config, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(config);
    }

    [HttpPost("Edit/{id:int}")]
    [HttpPut("{id:int}")]
    [HttpPost("/OrderBonusConfiguration/Edit")]
    public async Task<IActionResult> Edit([RouteOrRequest] int id, [FromBody] CreateOrderBonusConfigRequest request, CancellationToken ct = default)
    {
        var config = await _context.OrderBonusConfigurations.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (config == null) return NotFound("Bonus configuration not found.");

        config.Country = request.Country;
        config.OrderThreshold = request.OrderThreshold;
        config.FlatBonusAmount = request.FlatBonusAmount;
        config.PercentageBonus = request.PercentageBonus;
        config.EmployeeId = request.EmployeeId;

        await _context.SaveChangesAsync(ct);
        return Ok(config);
    }
}

public sealed record CreateOrderBonusConfigRequest(
    int Country,
    decimal OrderThreshold,
    decimal FlatBonusAmount,
    decimal? PercentageBonus,
    int? EmployeeId);
