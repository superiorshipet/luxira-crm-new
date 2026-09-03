using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/management-requests")]
[Route("ManagementRequests")]
public class ManagementRequestsController : ControllerBase
{
    private static readonly string[] RequestTypes = ["طلب غياب", "طلب ظرف طارئ", "طلب سلفة", "يوم غياب", "طلب اخر", "طلب استئذان بالساعات"];
    private readonly ApplicationDbContext _context;

    public ManagementRequestsController(ApplicationDbContext context) => _context = context;

    [HttpGet]
    [HttpGet("Index")]
    public IActionResult Index()
    {
        if (IsExternalDeliveryParty()) return Forbid();
        return Ok(new { isManagement = IsManagement(), requestTypes = RequestTypes });
    }

    [HttpGet("GetRequests")]
    public async Task<ActionResult<List<ManagementRequest>>> GetRequests(
        [FromQuery] string? applicationUserId, [FromQuery] string? status, CancellationToken ct)
    {
        var query = AccessibleRequests();
        if (!string.IsNullOrWhiteSpace(applicationUserId)) query = query.Where(request => request.ApplicationUserId == applicationUserId);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(request => request.Status == status);
        return Ok(await query.OrderByDescending(request => request.Id).ToListAsync(ct));
    }

    [HttpGet("List")]
    public async Task<IActionResult> List([FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate,
        [FromQuery] string? excuse, CancellationToken ct)
    {
        if (IsExternalDeliveryParty()) return Forbid();
        var query = AccessibleRequests();
        if (fromDate.HasValue) query = query.Where(item => item.CreatedAt >= fromDate.Value.Date);
        if (toDate.HasValue)
        {
            var end = toDate.Value.Date.AddDays(1);
            query = query.Where(item => item.CreatedAt < end);
        }
        if (!string.IsNullOrWhiteSpace(excuse))
        {
            var search = excuse.Trim();
            query = query.Where(item => item.Reason.Contains(search));
        }
        var rows = await query.OrderByDescending(item => item.Id).ToListAsync(ct);
        return Ok(new
        {
            success = true,
            isManagement = IsManagement(),
            pending = rows.Where(item => item.Status == "Pending").Select(ToClientItem),
            approved = rows.Where(item => item.Status == "Approved").Select(ToClientItem),
            rejected = rows.Where(item => item.Status == "Rejected").Select(ToClientItem)
        });
    }

    [HttpPost]
    [HttpPost("SubmitRequest")]
    public Task<IActionResult> SubmitRequest([FromBody] SubmitManagementRequest request, CancellationToken ct) =>
        SubmitCore(request.RequestType, request.Reason, ct);

    [HttpPost("Submit")]
    public Task<IActionResult> Submit([FromForm] string requestType, [FromForm] string reason,
        [FromForm] DateTime? requestDate, [FromForm] decimal? permissionHours,
        [FromForm] decimal? advanceAmount, CancellationToken ct)
    {
        var metadata = new List<string>();
        if (requestDate.HasValue) metadata.Add($"date={requestDate:yyyy-MM-dd}");
        if (permissionHours.HasValue) metadata.Add($"hours={permissionHours.Value:0.##}");
        if (advanceAmount.HasValue) metadata.Add($"advance={advanceAmount.Value:0.00}");
        var storedReason = metadata.Count == 0 ? reason : $"[MRPAY|{string.Join('|', metadata)}] {reason}";
        return SubmitCore(requestType, storedReason, ct);
    }

    [HttpPost("{id:int}/review")]
    [HttpPost("ReviewRequest/{id:int}")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
    public Task<IActionResult> ReviewRequest([RouteOrRequest] int id, [FromBody] ReviewManagementRequest request, CancellationToken ct) =>
        DecideCore(id, request.Approved ? "Approved" : "Rejected", ct);

    [HttpPost("Decide")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public Task<IActionResult> Decide([FromForm] long id, [FromForm] string decision, CancellationToken ct) => DecideCore(id, decision, ct);

    [HttpGet("GetNotifications")]
    public async Task<IActionResult> GetNotifications(CancellationToken ct)
    {
        if (IsExternalDeliveryParty()) return Ok(new { success = true, pendingCount = 0, items = Array.Empty<object>() });
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var items = await _context.ManagementRequestNotifications.AsNoTracking()
            .Where(item => item.ApplicationUserId == userId && !item.IsRead)
            .OrderBy(item => item.Id).Take(10)
            .Select(item => new
            {
                id = item.Id,
                requestId = item.ManagementRequestId,
                alertType = item.AlertType,
                title = item.Title,
                message = item.Message,
                requestType = item.ManagementRequest!.RequestType,
                reason = item.ManagementRequest.Reason,
                employeeName = item.ManagementRequest.EmployeeName,
                createdAt = item.CreatedAt
            }).ToListAsync(ct);
        var pendingCount = IsManagement() ? await _context.ManagementRequests.CountAsync(item => item.Status == "Pending", ct) : 0;
        return Ok(new { success = true, pendingCount, items });
    }

    [HttpPost("MarkNotificationRead")]
    public async Task<IActionResult> MarkNotificationRead([FromForm] long id, CancellationToken ct)
    {
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var changed = await _context.ManagementRequestNotifications
            .Where(item => item.Id == id && item.ApplicationUserId == userId && !item.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(item => item.IsRead, true).SetProperty(item => item.ReadAt, DateTime.UtcNow), ct);
        return Ok(new { success = true, changed = changed > 0 });
    }

    private async Task<IActionResult> SubmitCore(string? requestType, string? reason, CancellationToken ct)
    {
        if (IsExternalDeliveryParty() || IsManagement()) return Forbid();
        requestType = requestType?.Trim();
        reason = reason?.Trim();
        if (!RequestTypes.Contains(requestType, StringComparer.Ordinal))
            return BadRequest(new { success = false, message = "اختر نوع طلب صحيح." });
        if (string.IsNullOrWhiteSpace(reason)) return BadRequest(new { success = false, message = "يرجى كتابة سبب الطلب." });
        if (reason.Length > 2_000) return BadRequest(new { success = false, message = "سبب الطلب لا يمكن أن يتجاوز 2000 حرف." });

        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var identity = await _context.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => new { user.Email, user.Name, user.UserName }).SingleOrDefaultAsync(ct);
        if (identity is null) return Unauthorized();
        var employeeName = await _context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId == userId)
            .Select(employee => employee.DisplayName ?? employee.Name).FirstOrDefaultAsync(ct)
            ?? identity.Name ?? identity.UserName ?? identity.Email ?? "الموظف";
        var now = DateTime.UtcNow;
        var item = new ManagementRequest
        {
            ApplicationUserId = userId, EmployeeName = employeeName, EmployeeEmail = identity.Email,
            RequestType = requestType!, Reason = reason, Status = "Pending", CreatedAt = now
        };

        var managementIds = await _context.UserRoles.AsNoTracking()
            .Where(role => role.Role != null && (role.Role.Name == "Admin" || role.Role.Name == "ExecutiveDirector"))
            .Select(role => role.UserId).Distinct().ToListAsync(ct);
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        _context.ManagementRequests.Add(item);
        await _context.SaveChangesAsync(ct);
        var notifications = managementIds.Select(recipientId => NewNotification(item, recipientId,
            "management-request-new", "طلب جديد في انتظار موافقة الإدارة",
            $"يوجد طلب جديد من الموظف {employeeName} في انتظار الرد.\nنوع الطلب: {requestType}\nالسبب: {reason}", now)).ToList();
        var employeeNotification = NewNotification(item, userId, "management-request-submitted", "تم تقديم الطلب",
            "تم تقديم الطلب وفي انتظار موافقة الادارة وسوف نرد عليك خلال فترة قصيرة.", now);
        notifications.Add(employeeNotification);
        _context.ManagementRequestNotifications.AddRange(notifications);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true, message = employeeNotification.Message, item = ToClientItem(item), notification = employeeNotification });
    }

    private async Task<IActionResult> DecideCore(long id, string? decision, CancellationToken ct)
    {
        decision = decision?.Trim();
        if (decision is not ("Approved" or "Rejected"))
            return BadRequest(new { success = false, message = "قرار غير صالح." });
        var userId = User.GetUserId();
        if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
        var deciderName = User.Identity?.Name ?? "الإدارة";
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var item = await _context.ManagementRequests.SingleOrDefaultAsync(request => request.Id == id && request.Status == "Pending", ct);
        if (item is null) return Conflict(new { success = false, message = "هذا الطلب تم الرد عليه بالفعل أو لم يعد قيد المراجعة." });
        item.Status = decision;
        item.DecidedAt = DateTime.UtcNow;
        item.DecidedByUserId = userId;
        item.DecidedByName = deciderName;
        _context.ManagementRequestNotifications.Add(NewNotification(item, item.ApplicationUserId,
            decision == "Approved" ? "management-request-approved" : "management-request-rejected",
            decision == "Approved" ? "تمت الموافقة على طلبك" : "تم رفض طلبك",
            decision == "Approved" ? "تمت الموافقة على طلبك بنجاح." : "تم رفض طلبك من الإدارة.", item.DecidedAt.Value));
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true, message = decision == "Approved" ? "تمت الموافقة على الطلب بنجاح." : "تم رفض الطلب.", item = ToClientItem(item) });
    }

    private IQueryable<ManagementRequest> AccessibleRequests()
    {
        var query = _context.ManagementRequests.AsNoTracking();
        if (!IsManagement())
        {
            var userId = User.GetUserId();
            query = query.Where(item => item.ApplicationUserId == userId);
        }
        return query;
    }

    private bool IsManagement() => User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector");
    private bool IsExternalDeliveryParty() => User.IsInRole("DeliveryCompany") || User.IsInRole("DeliveryRepresentative");

    private static ManagementRequestNotification NewNotification(ManagementRequest request, string recipientId,
        string alertType, string title, string message, DateTime createdAt) => new()
    {
        ManagementRequest = request, ManagementRequestId = request.Id, ApplicationUserId = recipientId,
        AlertType = alertType, Title = title, Message = message, CreatedAt = createdAt
    };

    private static object ToClientItem(ManagementRequest item) => new
    {
        id = item.Id, applicationUserId = item.ApplicationUserId, employeeName = item.EmployeeName,
        employeeEmail = item.EmployeeEmail, requestType = item.RequestType, reason = item.Reason,
        status = item.Status, createdAt = item.CreatedAt, decidedAt = item.DecidedAt,
        decidedByUserId = item.DecidedByUserId, decidedByName = item.DecidedByName
    };
}

public sealed record SubmitManagementRequest(string RequestType, string Reason);
public sealed record ReviewManagementRequest(bool Approved);
