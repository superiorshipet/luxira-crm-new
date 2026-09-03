using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

public partial class OrderController
{
    [HttpGet("/Order/GetStatusUpdateMonitorDashboard")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetStatusUpdateMonitorDashboard(string? fromDate, string? toDate, CancellationToken ct)
    {
        var now = IstanbulTimeHelper.Now;
        var start = ParseMonitorDate(fromDate) ?? (now.Hour >= 10 ? now.Date.AddHours(10) : now.Date.AddDays(-1).AddHours(10));
        var end = ParseMonitorDate(toDate)?.AddDays(1) ?? start.AddDays(1);
        var logs = await _context.StatusUpdateBatchLogs.AsNoTracking()
            .Where(log => log.UpdatedAt >= start && log.UpdatedAt < end)
            .GroupBy(log => new { log.EmployeeUserId, log.EmployeeName, log.EmployeeImageUrl })
            .Select(group => new
            {
                employeeUserId = group.Key.EmployeeUserId,
                employeeName = group.Key.EmployeeName ?? string.Empty,
                employeeImageUrl = group.Key.EmployeeImageUrl ?? "/static/DefaultImage.svg",
                batchCount = group.Count(),
                orderCount = group.Sum(log => log.OrderCount),
                lastUpdatedAt = group.Max(log => log.UpdatedAt)
            }).OrderByDescending(row => row.lastUpdatedAt).ToListAsync(ct);
        return Ok(new { success = true, from = start, to = end, rows = logs });
    }

    [HttpGet("/Order/GetStatusUpdateBatchLogs")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetStatusUpdateBatchLogs(int take = 80, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 10, 200);
        var logs = await _context.StatusUpdateBatchLogs.AsNoTracking().Include(log => log.Items)
            .OrderByDescending(log => log.UpdatedAt).ThenByDescending(log => log.Id).Take(take * 3).ToListAsync(ct);
        var rows = logs.GroupBy(log => log.BatchKey).Select(group =>
        {
            var first = group.OrderByDescending(log => log.Id).First();
            var items = group.SelectMany(log => log.Items).GroupBy(item => new
                { item.OrderId, item.FinalStatusValue, item.FailureReason, item.UpdatedAt }).Select(itemGroup => itemGroup.First()).ToList();
            return new
            {
                id = first.Id,
                batchKey = first.BatchKey,
                employeeName = first.EmployeeName ?? string.Empty,
                employeeImageUrl = first.EmployeeImageUrl ?? "/static/DefaultImage.svg",
                countryName = JoinDistinct(items.Select(item => item.CountryName)),
                storeName = JoinDistinct(items.Select(item => item.StoreName)),
                finalStatusName = items.Select(item => item.FinalStatusName).Distinct().Count() == 1 ? items[0].FinalStatusName : "متعدد",
                orderCount = items.Count,
                updatedAt = group.Max(log => log.UpdatedAt),
                breakdown = items.GroupBy(item => new { item.CountryName, item.StoreName, item.FinalStatusName, item.FinalStatusValue })
                    .Select(itemGroup => new { itemGroup.Key.CountryName, itemGroup.Key.StoreName, itemGroup.Key.FinalStatusName, itemGroup.Key.FinalStatusValue, orderCount = itemGroup.Count() })
            };
        }).OrderByDescending(row => row.updatedAt).Take(take).ToList();
        return Ok(new { success = true, rows });
    }

    [HttpGet("/Order/GetStatusUpdateBatchLogDetails")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetStatusUpdateBatchLogDetails(int batchLogId, Guid? batchKey, CancellationToken ct)
    {
        var selected = batchKey.HasValue
            ? await _context.StatusUpdateBatchLogs.AsNoTracking().FirstOrDefaultAsync(log => log.BatchKey == batchKey.Value, ct)
            : await _context.StatusUpdateBatchLogs.AsNoTracking().FirstOrDefaultAsync(log => log.Id == batchLogId, ct);
        if (selected is null) return Ok(new { success = false, message = "السجل غير موجود." });
        var ids = await _context.StatusUpdateBatchLogs.AsNoTracking().Where(log => log.BatchKey == selected.BatchKey).Select(log => log.Id).ToListAsync(ct);
        var rows = await _context.StatusUpdateBatchLogItems.AsNoTracking().Where(item => ids.Contains(item.BatchLogId))
            .OrderBy(item => item.Id).Select(item => new
            {
                item.OrderCode, item.FinalStatusName, failureReason = item.FailureReason ?? string.Empty,
                deliveryCompanyName = item.DeliveryCompanyName ?? string.Empty, countryName = item.CountryName ?? string.Empty,
                storeName = item.StoreName ?? string.Empty, item.UpdatedAt
            }).ToListAsync(ct);
        var header = new { selected.Id, selected.BatchKey, selected.EmployeeName, selected.EmployeeImageUrl, selected.FinalStatusName, orderCount = rows.Count, selected.UpdatedAt };
        return Ok(new { success = true, header, rows });
    }

    [HttpPost("/Order/DeleteStatusUpdateBatchHistory")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> DeleteStatusUpdateBatchHistory([FromForm] int batchLogId, [FromForm] Guid? batchKey, CancellationToken ct)
    {
        var selected = batchKey.HasValue
            ? await _context.StatusUpdateBatchLogs.AsNoTracking().FirstOrDefaultAsync(log => log.BatchKey == batchKey.Value, ct)
            : await _context.StatusUpdateBatchLogs.AsNoTracking().FirstOrDefaultAsync(log => log.Id == batchLogId, ct);
        if (selected is null) return Ok(new { success = false, message = "السجل غير موجود أو تم حذفه بالفعل." });
        var logs = await _context.StatusUpdateBatchLogs.Include(log => log.Items).Where(log => log.BatchKey == selected.BatchKey).ToListAsync(ct);
        var removedItems = logs.Sum(log => log.Items.Count);
        _context.StatusUpdateBatchLogs.RemoveRange(logs);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, removedLogs = logs.Count, removedItems, selected.BatchKey });
    }

    [HttpPost("/Order/CleanDuplicateStatusUpdateBatchHistory")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> CleanDuplicateStatusUpdateBatchHistory(CancellationToken ct)
    {
        var logs = await _context.StatusUpdateBatchLogs.Include(log => log.Items)
            .OrderBy(log => log.UpdatedAt).ThenBy(log => log.Id).ToListAsync(ct);
        var removedItems = 0;
        foreach (var log in logs)
        {
            var duplicates = log.Items.GroupBy(item => new { item.OrderId, item.FinalStatusValue, item.FailureReason, item.UpdatedAt })
                .SelectMany(group => group.Skip(1)).ToList();
            _context.StatusUpdateBatchLogItems.RemoveRange(duplicates);
            removedItems += duplicates.Count;
        }
        await _context.SaveChangesAsync(ct);
        var emptyLogs = logs.Where(log => !log.Items.Any(item => _context.Entry(item).State != EntityState.Deleted)).ToList();
        _context.StatusUpdateBatchLogs.RemoveRange(emptyLogs);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, removedLogs = emptyLogs.Count, removedItems });
    }

    private async Task RecordStatusUpdateBatchAsync(IEnumerable<int> orderIds, int status, string? reason, CancellationToken ct)
    {
        var ids = orderIds.Where(id => id > 0).Distinct().ToArray();
        if (ids.Length == 0) return;
        var now = IstanbulTimeHelper.Now;
        var rows = await _context.Orders.AsNoTracking().Where(order => ids.Contains(order.Id)).Select(order => new
        {
            order.Id, order.ExternalOrderId, order.Country, order.ManufacturingCompanyId,
            DeliveryCompanyName = order.DeliveryCompany != null ? order.DeliveryCompany.Name : null,
            StoreName = _context.ManufacturingCompanies
                .Where(company => company.Id == order.ManufacturingCompanyId)
                .Select(company => company.Name)
                .FirstOrDefault()
        }).ToListAsync(ct);
        if (rows.Count == 0) return;
        var userId = User.GetUserId();
        var profile = await _context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId == userId)
            .Select(employee => new { Name = employee.DisplayName ?? employee.Name, employee.ImageUrl }).FirstOrDefaultAsync(ct);
        var statusName = status.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var items = rows.Select(row => new StatusUpdateBatchLogItem
        {
            OrderId = row.Id,
            OrderCode = (row.ExternalOrderId ?? row.Id).ToString(System.Globalization.CultureInfo.InvariantCulture),
            FinalStatusValue = status,
            FinalStatusName = statusName,
            FailureReason = reason,
            DeliveryCompanyName = row.DeliveryCompanyName,
            CountryName = row.Country.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StoreName = row.StoreName,
            UpdatedAt = now
        }).ToList();
        var signatureIds = items.Select(item => item.OrderId).Order().ToArray();
        var duplicateStart = now.AddSeconds(-8);
        var recent = await _context.StatusUpdateBatchLogs.AsNoTracking().Include(log => log.Items)
            .Where(log => log.EmployeeUserId == userId && log.OrderCount == items.Count && log.UpdatedAt >= duplicateStart).ToListAsync(ct);
        if (recent.Any(log => log.FinalStatusValue == status && log.Items.Select(item => item.OrderId).Order().SequenceEqual(signatureIds))) return;
        var storeIds = rows.Select(row => row.ManufacturingCompanyId).Distinct().ToArray();
        _context.StatusUpdateBatchLogs.Add(new StatusUpdateBatchLog
        {
            BatchKey = Guid.NewGuid(), EmployeeUserId = userId, EmployeeName = profile?.Name ?? User.Identity?.Name,
            EmployeeImageUrl = profile?.ImageUrl ?? "/static/DefaultImage.svg", CountryName = JoinDistinct(items.Select(item => item.CountryName)),
            StoreId = storeIds.Length == 1 ? storeIds[0] : null, StoreName = JoinDistinct(items.Select(item => item.StoreName)),
            FinalStatusValue = status, FinalStatusName = statusName, OrderCount = items.Count, UpdatedAt = now, Items = items
        });
        await _context.SaveChangesAsync(ct);
    }

    private static DateTime? ParseMonitorDate(string? value) => DateTime.TryParse(value, out var result) ? result.Date.AddHours(10) : null;
    private static string JoinDistinct(IEnumerable<string?> values) => string.Join("، ", values.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct().Order());
}
