using Luxira.Api.Data;
using Luxira.Api.Features.Expenses.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Marketing.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Marketing")]
[Route("api/v1/marketing/sales-indicators")]
[Route("SalesIndicators")]
public class SalesIndicatorsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public SalesIndicatorsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/SalesIndicators/Index")]
    public async Task<IActionResult> Index([FromQuery] int? countryId, CancellationToken ct = default)
    {
        var query = _context.SalesIndicators.AsNoTracking().AsQueryable();
        if (countryId.HasValue) query = query.Where(s => s.Country == countryId.Value);

        var indicators = await query.ToListAsync(ct);
        return Ok(indicators);
    }

    [HttpPost("Save")]
    [HttpPost("/SalesIndicators/Save")]
    public async Task<IActionResult> Save([FromBody] SalesIndicatorViewModel model, CancellationToken ct = default)
    {
        var indicator = await _context.SalesIndicators
            .FirstOrDefaultAsync(s => s.Country == model.Country && s.Month == model.Month && s.Year == model.Year, ct);

        if (indicator == null)
        {
            indicator = new SalesIndicator
            {
                Country = model.Country,
                Month = model.Month,
                Year = model.Year,
                TargetAmount = model.TargetAmount
            };
            await _context.SalesIndicators.AddAsync(indicator, ct);
        }
        else
        {
            indicator.TargetAmount = model.TargetAmount;
        }

        await _context.SaveChangesAsync(ct);
        return Ok(indicator);
    }

    [HttpPost("Delete/{id:int}")]
    [HttpDelete("{id:int}")]
    [HttpPost("/SalesIndicators/Delete")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct = default)
    {
        var indicator = await _context.SalesIndicators.FirstOrDefaultAsync(s => s.Id == id, ct);
        if (indicator == null) return NotFound("Sales indicator not found.");

        _context.SalesIndicators.Remove(indicator);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }
}

public record SalesIndicatorViewModel(int Country, int Month, int Year, decimal TargetAmount);
