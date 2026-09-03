using Luxira.Api.Data;
using System.Diagnostics;
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

    [HttpGet("Index")]
    [HttpPost("Index")]
    public IActionResult Index() => Ok(new { seedScriptTestMessage = "alert('hello world');" });

    [HttpGet("CompareHomepageSearch")]
    public async Task<IActionResult> CompareHomepageSearch([FromQuery] string search, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(search)) return BadRequest(new { message = "search is required" });
        var normalized = NormalizePhone(search);
        var unboundedWatch = Stopwatch.StartNew();
        var unboundedCount = await _context.Orders.AsNoTracking().CountAsync(order =>
            order.TelephoneNumber.Contains(normalized)
            || order.SecondTelephoneNumber != null && order.SecondTelephoneNumber.Contains(normalized), ct);
        unboundedWatch.Stop();

        var twoMonthsAgo = DateTime.UtcNow.AddMonths(-2);
        var boundedWatch = Stopwatch.StartNew();
        var boundedCount = await _context.Orders.AsNoTracking().CountAsync(order => order.InstantAddedDate >= twoMonthsAgo
            && (order.TelephoneNumber.Contains(normalized)
                || order.SecondTelephoneNumber != null && order.SecondTelephoneNumber.Contains(normalized)), ct);
        boundedWatch.Stop();
        return Ok(new
        {
            search = search.Trim().ToLowerInvariant(),
            unbounded = new { resultCount = unboundedCount, elapsedMs = unboundedWatch.ElapsedMilliseconds },
            lastTwoMonths = new { fromDate = twoMonthsAgo, resultCount = boundedCount, elapsedMs = boundedWatch.ElapsedMilliseconds }
        });
    }

    [HttpPost("DryRunPoPhoneNormalization")]
    public IActionResult DryRunPoPhoneNormalization() =>
        Ok(new { message = "TODO: implement dry-run logic for PotentialOrder.PhoneNumber normalization." });

    [HttpPost("ApplyPoPhoneNormalization")]
    public IActionResult ApplyPoPhoneNormalization() =>
        Ok(new { message = "TODO: implement apply logic for PotentialOrder.PhoneNumber normalization." });

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

    private static string NormalizePhone(string value) => new(value.Where(char.IsDigit).ToArray());
}
