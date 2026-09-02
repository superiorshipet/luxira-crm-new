using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/operations/admin-dashboard")]
[Route("AdminDashboard")]
public class AdminDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public AdminDashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetSummary")]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var now = DateTimeOffset.UtcNow;
        int activeUsers = await _context.Users.CountAsync(
            u => !u.LockoutEnd.HasValue || u.LockoutEnd <= now,
            ct);
        int totalEmployees = await _context.Employees.CountAsync(e => e.IsActive, ct);
        int totalWarehouses = await _context.Warehouses.CountAsync(w => w.IsShown, ct);
        int totalStores = await _context.ManufacturingCompanies.CountAsync(m => m.IsShown, ct);
        int totalDeliveryCompanies = await _context.DeliveryCompanies.CountAsync(d => d.IsActive, ct);

        return Ok(new
        {
            activeUsers,
            totalEmployees,
            totalWarehouses,
            totalStores,
            totalDeliveryCompanies
        });
    }
}
