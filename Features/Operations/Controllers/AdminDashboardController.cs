using Luxira.Api.Data;
using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace Luxira.Api.Features.Operations.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator")]
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
        var normalized = NormalizePhone(search)!;
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
    public async Task<IActionResult> DryRunPoPhoneNormalization(CancellationToken ct)
    {
        var rows = await _context.PotentialOrders
            .AsNoTracking()
            .Select(order => new { order.Id, order.PhoneNumber })
            .ToListAsync(ct);
        var changes = rows
            .Select(order => new { order.Id, Before = order.PhoneNumber, After = NormalizePhone(order.PhoneNumber) })
            .Where(order => !string.Equals(order.Before, order.After, StringComparison.Ordinal))
            .ToList();

        return Ok(new
        {
            rowsScanned = rows.Count,
            rowsThatWouldChange = changes.Count,
            sample = changes.Take(25).Select(order => new { order.Id, before = order.Before, after = order.After })
        });
    }

    [HttpPost("ApplyPoPhoneNormalization")]
    public async Task<IActionResult> ApplyPoPhoneNormalization(CancellationToken ct)
    {
        var rows = await _context.PotentialOrders.ToListAsync(ct);
        var changed = 0;
        foreach (var order in rows)
        {
            var normalized = NormalizePhone(order.PhoneNumber);
            if (string.Equals(order.PhoneNumber, normalized, StringComparison.Ordinal)) continue;
            order.PhoneNumber = normalized;
            changed++;
        }

        if (changed > 0) await _context.SaveChangesAsync(ct);
        return Ok(new { rowsScanned = rows.Count, rowsChanged = changed });
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

    internal static string? NormalizePhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        var builder = new StringBuilder(raw.Length);
        foreach (var character in raw)
        {
            if (character is >= '٠' and <= '٩') builder.Append((char)('0' + character - '٠'));
            else if (character is >= '۰' and <= '۹') builder.Append((char)('0' + character - '۰'));
            else builder.Append(character);
        }

        var cleaned = builder.ToString()
            .Replace("⁦", string.Empty, StringComparison.Ordinal)
            .Replace("⁧", string.Empty, StringComparison.Ordinal)
            .Replace("⁨", string.Empty, StringComparison.Ordinal)
            .Replace("⁩", string.Empty, StringComparison.Ordinal)
            .Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);

        foreach (var (dialCode, noLeadingZero) in CountryDialCodes)
        {
            var internationalPrefix = "00" + dialCode;
            if (cleaned.StartsWith(internationalPrefix, StringComparison.Ordinal) && cleaned.Length > internationalPrefix.Length)
                return (noLeadingZero ? string.Empty : "0") + cleaned[internationalPrefix.Length..];

            var plusPrefix = "+" + dialCode;
            if (cleaned.StartsWith(plusPrefix, StringComparison.Ordinal) && cleaned.Length > plusPrefix.Length)
                return (noLeadingZero ? string.Empty : "0") + cleaned[plusPrefix.Length..];
        }

        return cleaned;
    }

    private static readonly (string DialCode, bool NoLeadingZero)[] CountryDialCodes =
    [
        ("964", false), ("971", false), ("974", true), ("218", false),
        ("968", true), ("970", false), ("962", false), ("965", true),
        ("973", true), ("966", false), ("216", true), ("212", false),
        ("213", false), ("961", false), ("20", false), ("90", false)
    ];
}
