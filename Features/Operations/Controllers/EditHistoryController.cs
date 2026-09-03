using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
[Route("api/v1/operations/edit-history")]
[Route("EditHistory")]
public class EditHistoryController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EditHistoryController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("AllEdits")]
    [HttpGet("/EditHistory/AllEdits")]
    public async Task<IActionResult> AllEdits([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var query = _context.OrderEditHistories
            .Include(h => h.Order)
            .OrderByDescending(h => h.EditNumber)
            .AsNoTracking();

        var total = await query.CountAsync(ct);
        var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);

        return Ok(new { total, page, pageSize, items });
    }

    [HttpGet("OrderChanges/{id:int}")]
    [HttpGet("/EditHistory/OrderChanges")]
    public async Task<IActionResult> OrderChanges([RouteOrRequest] int id, [FromQuery] int? orderId, CancellationToken ct = default)
    {
        var targetId = id > 0 ? id : (orderId ?? 0);
        var changes = await _context.OrderEditHistories
            .Where(h => h.OrderId == targetId)
            .OrderByDescending(h => h.EditNumber)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(changes);
    }

    [HttpGet("EmployeeSearch")]
    [HttpGet("/EditHistory/EmployeeSearch")]
    public async Task<IActionResult> EmployeeSearch([FromQuery] string q, CancellationToken ct = default)
    {
        var employees = await _context.Employees
            .Where(e => e.Name.Contains(q) || e.PhoneNumber.Contains(q))
            .Select(e => new { e.Id, e.Name, e.PhoneNumber, e.JobTitle })
            .Take(20)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(employees);
    }
}
