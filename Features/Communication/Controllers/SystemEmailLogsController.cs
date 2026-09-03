using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize(Roles = "Admin,ExecutiveDirector")]
[Route("SystemEmailLogs")]
[Route("api/v1/communication/email-logs")]
public sealed class SystemEmailLogsController : ControllerBase
{
    private const int PageSize = 30;
    private readonly ApplicationDbContext _context;

    public SystemEmailLogsController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        [FromQuery] string? search,
        [FromQuery] string? recipient,
        [FromQuery] string? emailType,
        [FromQuery] string? status,
        [FromQuery] string? direction,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        search = Clean(search);
        recipient = Clean(recipient);
        emailType = Clean(emailType);
        status = Clean(status);
        direction = NormalizeDirection(direction);
        fromDate = fromDate?.Date;
        toDate = toDate?.Date;
        if (fromDate.HasValue && !toDate.HasValue) toDate = fromDate;
        else if (!fromDate.HasValue && toDate.HasValue) fromDate = toDate;
        if (fromDate > toDate) (fromDate, toDate) = (toDate, fromDate);
        page = Math.Max(1, page);

        var query = _context.SystemEmailLogs.AsNoTracking().AsQueryable();
        if (search is not null)
            query = query.Where(log => log.ToEmail.Contains(search)
                || log.FromEmail != null && log.FromEmail.Contains(search)
                || log.Subject.Contains(search)
                || log.RecipientName != null && log.RecipientName.Contains(search)
                || log.TriggeredByName != null && log.TriggeredByName.Contains(search)
                || log.RelatedEntityId != null && log.RelatedEntityId.Contains(search)
                || log.BodyPreview != null && log.BodyPreview.Contains(search));
        if (recipient is not null) query = query.Where(log => log.RecipientName == recipient);
        if (emailType is not null) query = query.Where(log => log.EmailType == emailType);
        if (status is not null) query = query.Where(log => log.Status == status);
        if (direction == SystemEmailLog.DirectionIncoming)
            query = query.Where(log => log.Direction == SystemEmailLog.DirectionIncoming);
        else if (direction == SystemEmailLog.DirectionOutgoing)
            query = query.Where(log => log.Direction == null || log.Direction == "" || log.Direction == SystemEmailLog.DirectionOutgoing);
        if (fromDate.HasValue) query = query.Where(log => log.SentAt >= fromDate.Value);
        if (toDate.HasValue)
        {
            var endExclusive = toDate.Value.AddDays(1);
            query = query.Where(log => log.SentAt < endExclusive);
        }

        var totalCount = await query.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)PageSize));
        page = Math.Min(page, totalPages);
        var items = await query.OrderByDescending(log => log.SentAt).ThenByDescending(log => log.Id)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(ct);

        var today = IstanbulTimeHelper.Now.Date;
        var tomorrow = today.AddDays(1);
        var counts = await _context.SystemEmailLogs.AsNoTracking().GroupBy(_ => 1).Select(group => new
        {
            sentCount = group.Count(log => (log.Direction == null || log.Direction == "" || log.Direction == SystemEmailLog.DirectionOutgoing) && log.Status == "Sent"),
            receivedCount = group.Count(log => log.Direction == SystemEmailLog.DirectionIncoming && log.Status == "Received"),
            failedCount = group.Count(log => (log.Direction == null || log.Direction == "" || log.Direction == SystemEmailLog.DirectionOutgoing) && log.Status == "Failed"),
            todayCount = group.Count(log => log.SentAt >= today && log.SentAt < tomorrow)
        }).SingleOrDefaultAsync(ct);
        var emailTypes = await _context.SystemEmailLogs.AsNoTracking().Where(log => log.EmailType != "")
            .Select(log => log.EmailType).Distinct().OrderBy(value => value).ToListAsync(ct);
        var recipientNames = await _context.SystemEmailLogs.AsNoTracking().Where(log => log.RecipientName != null && log.RecipientName != "")
            .Select(log => log.RecipientName!).Distinct().OrderBy(value => value).Take(500).ToListAsync(ct);

        return Ok(new
        {
            items, search, recipient, emailType, status, direction, fromDate, toDate,
            page, pageSize = PageSize, totalCount, totalPages,
            sentCount = counts?.sentCount ?? 0,
            receivedCount = counts?.receivedCount ?? 0,
            failedCount = counts?.failedCount ?? 0,
            todayCount = counts?.todayCount ?? 0,
            emailTypes, recipientNames
        });
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeDirection(string? direction)
    {
        var value = Clean(direction);
        if (value?.Equals(SystemEmailLog.DirectionIncoming, StringComparison.OrdinalIgnoreCase) == true)
            return SystemEmailLog.DirectionIncoming;
        return value?.Equals(SystemEmailLog.DirectionOutgoing, StringComparison.OrdinalIgnoreCase) == true
            ? SystemEmailLog.DirectionOutgoing
            : null;
    }
}
