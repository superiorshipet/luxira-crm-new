using System.Text.Json;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

public partial class OrderController
{
    [HttpPost("/Order/QuickReportCreate")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    [RequestSizeLimit(30 * 1024 * 1024)]
    public async Task<IActionResult> QuickReportCreate([FromForm] string? orderCode, [FromForm] string? orderId, [FromForm] int type, [FromForm] string? body, [FromForm] string? reason, [FromForm] List<IFormFile>? images, CancellationToken ct)
    {
        var code = (orderCode ?? orderId ?? string.Empty).Trim();
        var text = (body ?? reason ?? string.Empty).Trim();
        var files = (images ?? []).Where(file => file.Length > 0).ToList();
        if (string.IsNullOrWhiteSpace(code)) return BadRequest(new { success = false, message = "تعذر تحديد الطلب المفتوح" });
        if (string.IsNullOrWhiteSpace(text) && files.Count == 0) return BadRequest(new { success = false, message = "اكتب سبب البلاغ أو اختار صورة" });
        var digits = new string(code.Where(char.IsDigit).ToArray());
        Order? order = null;
        if (int.TryParse(digits, out var numeric)) order = await _context.Orders.AsNoTracking().Where(item => item.Id == numeric || item.ExternalOrderId == numeric).OrderByDescending(item => item.Id).FirstOrDefaultAsync(ct);
        if (order is null && digits.Length >= 7) { var suffix = digits.Length > 9 ? digits[^9..] : digits; order = await _context.Orders.AsNoTracking().Where(item => item.TelephoneNumber.EndsWith(suffix) || (item.SecondTelephoneNumber != null && item.SecondTelephoneNumber.EndsWith(suffix))).OrderByDescending(item => item.Id).FirstOrDefaultAsync(ct); }
        if (order is null) return BadRequest(new { success = false, message = "الطلب غير موجود أو ليس لديك صلاحية لإرسال تبليغ عليه" });
        var post = new OrderPost { OrderId = order.Id, Type = type == 1 ? OrderPostType.EditNote : OrderPostType.Problem, Body = string.IsNullOrWhiteSpace(text) ? null : text, AuthorUserId = User.GetUserId() ?? "system", CreatedAt = IstanbulTimeHelper.Now };
        _context.OrderPosts.Add(post); await _context.SaveChangesAsync(ct);
        var errors = new List<string>(); var sort = 0;
        foreach (var file in files)
        {
            if (!(file.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false)) { errors.Add($"الملف {file.FileName} ليس صورة مدعومة"); continue; }
            try { var stored = await _storage.UploadAsync(file, "orderposts", User.GetUserId(), ct); _context.OrderPostImages.Add(new OrderPostImage { OrderPostId = post.Id, Url = stored.PublicUrl ?? $"/OrderPosts/Image?key={Uri.EscapeDataString(stored.Key)}", S3Key = stored.Key, SortOrder = sort++ }); }
            catch { errors.Add($"تعذر رفع الصورة {file.FileName}"); }
        }
        await _context.SaveChangesAsync(ct);
        await _hub.Clients.Group("OrderPostListeners").SendAsync("newOrderPost", order.Id, (int)post.Type, ct);
        return Ok(new { success = true, postId = post.Id, orderId = order.Id, uploadedImageCount = sort, imageUploadErrors = errors });
    }

    [HttpGet("/Order/QuickReportList")]
    public async Task<IActionResult> QuickReportList([FromQuery] int? orderId, [FromQuery] int? type, CancellationToken ct)
    {
        var userId = User.GetUserId(); var canManage = User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector") || User.IsInRole("FollowUpDepartment");
        var query = _context.OrderPosts.AsNoTracking().Where(post => post.Type == OrderPostType.Problem || post.Type == OrderPostType.EditNote);
        if (orderId.HasValue) query = query.Where(post => post.OrderId == orderId); if (type.HasValue) query = query.Where(post => (int)post.Type == type);
        if (!canManage && !orderId.HasValue) query = query.Where(post => post.AuthorUserId == userId);
        var rows = await query.OrderByDescending(post => post.CreatedAt).Take(500).Select(post => new { post.Id, post.OrderId, type = (int)post.Type, post.Body, post.AuthorUserId, post.CreatedAt, imageUrls = post.Images.OrderBy(image => image.SortOrder).Select(image => image.Url), canDelete = canManage || post.AuthorUserId == userId }).ToListAsync(ct);
        return Ok(new { success = true, items = rows, count = rows.Count });
    }

    [HttpPost("/Order/QuickReportDelete")]
    public async Task<IActionResult> QuickReportDelete([FromForm] int id, CancellationToken ct)
    {
        var post = await _context.OrderPosts.Include(item => item.Images).FirstOrDefaultAsync(item => item.Id == id, ct); if (post is null) return NotFound(new { success = false });
        var canManage = User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector") || User.IsInRole("FollowUpDepartment"); if (!canManage && post.AuthorUserId != User.GetUserId()) return Forbid();
        _context.OrderPostDeletedHistories.Add(new OrderPostDeletedHistory { OrderPostId = post.Id, OrderId = post.OrderId, Type = (int)post.Type, Body = post.Body, AuthorUserId = post.AuthorUserId, CreatedAt = post.CreatedAt, DeletedAt = IstanbulTimeHelper.Now, DeletedByUserId = User.GetUserId(), DeletedByName = User.Identity?.Name });
        foreach (var image in post.Images) if (!string.IsNullOrWhiteSpace(image.S3Key)) { try { await _storage.DeleteAsync(image.S3Key, ct); } catch { } }
        _context.OrderPosts.Remove(post); await _context.SaveChangesAsync(ct); return Ok(new { success = true, id });
    }

    [HttpGet("/Order/GetProblemShareContext")]
    [HttpGet("/Order/GetProblemShareEmployees")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> GetProblemShareContext([FromQuery] int postId = 0, [FromQuery] int id = 0, [FromQuery] int? orderId = null, CancellationToken ct = default)
    {
        var post = await _context.OrderPosts.AsNoTracking().Include(item => item.Images).FirstOrDefaultAsync(item => item.Id == (postId > 0 ? postId : id) && item.Type == OrderPostType.Problem, ct);
        var resolvedOrderId = post?.OrderId ?? orderId ?? 0; if (resolvedOrderId <= 0) return NotFound(new { success = false });
        var employees = await ProblemParticipants(resolvedOrderId, ct);
        return Ok(new { success = true, id = post?.Id ?? 0, postId = post?.Id ?? 0, orderId = resolvedOrderId, body = post?.Body ?? string.Empty, text = post?.Body ?? string.Empty, imageUrls = post?.Images.OrderBy(image => image.SortOrder).Select(image => image.Url).ToList() ?? [], employees });
    }

    [HttpGet("/Order/GetProblemDeductionParticipants")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> GetProblemDeductionParticipants([FromQuery] int postId, [FromQuery] int? orderId, CancellationToken ct)
    {
        var post = await _context.OrderPosts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == postId && item.Type == OrderPostType.Problem, ct); var resolved = post?.OrderId ?? orderId ?? 0;
        var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == resolved, ct); if (order is null) return NotFound(new { success = false });
        var amountEgp = await ConvertOrderAmountToEgp(order.TotalPrice, order.Country, ct); var participants = await ProblemParticipants(order.Id, ct); var participantIds = participants.Select(item => item.id).ToArray();
        var others = await _context.Employees.AsNoTracking().Where(item => item.IsActive && item.IsShown && !item.IsDeleted && !participantIds.Contains(item.Id)).Select(item => new { id = item.Id, employeeId = item.Id, applicationUserId = item.ApplicationUserId, name = item.DisplayName ?? item.Name, item.ImageUrl, item.Country }).Take(500).ToListAsync(ct);
        return Ok(new { success = true, postId, orderId = order.Id, totalPrice = amountEgp, amountEgp, sourceAmount = order.TotalPrice, sourceCountry = order.Country, reportBody = post?.Body ?? string.Empty, employees = participants, otherEmployees = others });
    }

    [HttpPost("/Order/ApplyProblemReportDeduction")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> ApplyProblemReportDeduction([FromForm] int postId, [FromForm] int? orderId, [FromForm] decimal amount, [FromForm] string? reason, [FromForm] string? employeeIds, CancellationToken ct)
    {
        var ids = ParseIntList(employeeIds); if (ids.Count == 0) return BadRequest(new { success = false, message = "اختاري موظفًا واحدًا على الأقل" });
        var post = await _context.OrderPosts.AsNoTracking().FirstOrDefaultAsync(item => item.Id == postId && item.Type == OrderPostType.Problem, ct); var resolved = post?.OrderId ?? orderId ?? 0;
        var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == resolved, ct); if (order is null) return NotFound(new { success = false });
        var authoritativeAmount = await ConvertOrderAmountToEgp(order.TotalPrice, order.Country, ct); if (authoritativeAmount <= 0) authoritativeAmount = Math.Abs(amount); if (authoritativeAmount <= 0) return BadRequest(new { success = false });
        var employees = await _context.Employees.Where(item => ids.Contains(item.Id) && item.IsActive && !item.IsDeleted).ToListAsync(ct); if (employees.Count != ids.Count) return BadRequest(new { success = false, message = "يوجد موظف مختار غير متاح." });
        var storedReason = $"خصم بلاغ مشكلة - الطلب #{order.Id} - الرسالة #{postId}: {(string.IsNullOrWhiteSpace(reason) ? post?.Body : reason)}"; var now = IstanbulTimeHelper.Now;
        foreach (var employee in employees)
        {
            var transaction = new EmployeeTransaction { EmployeeId = employee.Id, Amount = authoritativeAmount, TransactionType = EmployeeTransactionType.Deduction, Reason = storedReason, Date = now };
            _context.EmployeeTransactions.Add(transaction); await _context.SaveChangesAsync(ct);
            _context.OrderPostEmployeeDeductions.Add(new OrderPostEmployeeDeduction { OrderId = order.Id, EmployeeId = employee.Id, EmployeeName = employee.DisplayName ?? employee.Name, Amount = authoritativeAmount, OrderTotal = order.TotalPrice, Reason = storedReason, ProblemText = post?.Body, CreatedAt = now, CreatedByUserId = User.GetUserId(), CreatedByName = User.Identity?.Name, EmployeeTransactionId = transaction.Id });
        }
        await _context.SaveChangesAsync(ct); return Ok(new { success = true, employeeCount = employees.Count, amountEgp = authoritativeAmount });
    }

    [HttpPost("/Order/ShareProblemPost")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,CallCenter")]
    public async Task<IActionResult> ShareProblemPost([FromForm] int postId, [FromForm] string? destination, [FromForm] string? destinations, [FromForm] string? targetApplicationUserId, [FromForm] string? targetApplicationUserIds, CancellationToken ct)
    {
        var source = await _context.OrderPosts.Include(item => item.Images).FirstOrDefaultAsync(item => item.Id == postId && item.Type == OrderPostType.Problem, ct); if (source is null) return NotFound(new { success = false });
        var destinationList = ParseStringList(destinations); if (!string.IsNullOrWhiteSpace(destination)) destinationList.AddRange(destination == "all-three" ? ["edit", "employee-error"] : [destination]); destinationList = destinationList.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        if (destinationList.Any(item => item is not ("edit" or "employee-error"))) return BadRequest(new { success = false });
        var targetIds = ParseStringList(targetApplicationUserIds); if (!string.IsNullOrWhiteSpace(targetApplicationUserId)) targetIds.Add(targetApplicationUserId); if (targetIds.Count == 0) { var creator = await _context.Orders.Where(item => item.Id == source.OrderId).Select(item => item.ApplicationUserId).FirstOrDefaultAsync(ct); if (!string.IsNullOrWhiteSpace(creator)) targetIds.Add(creator); }
        var employees = await _context.Employees.Where(item => item.ApplicationUserId != null && targetIds.Contains(item.ApplicationUserId)).ToListAsync(ct); var createdPosts = new List<int>(); var createdErrors = new List<int>();
        foreach (var employee in employees)
        {
            if (destinationList.Contains("edit", StringComparer.OrdinalIgnoreCase)) { var post = new OrderPost { OrderId = source.OrderId, Type = OrderPostType.EditNote, AuthorUserId = employee.ApplicationUserId!, Body = source.Body, CreatedAt = IstanbulTimeHelper.Now }; _context.OrderPosts.Add(post); await _context.SaveChangesAsync(ct); foreach (var image in source.Images) _context.OrderPostImages.Add(new OrderPostImage { OrderPostId = post.Id, Url = image.Url, S3Key = image.S3Key, SortOrder = image.SortOrder }); createdPosts.Add(post.Id); }
            if (destinationList.Contains("employee-error", StringComparer.OrdinalIgnoreCase)) { var error = new EmployeeError { EmployeeId = employee.Id, EmployeeNameSnapshot = employee.DisplayName ?? employee.Name, ErrorText = source.Body ?? "بلاغ عن مشكلة", ImageUrl = source.Images.FirstOrDefault()?.Url, CreatedAt = IstanbulTimeHelper.Now, CreatedByUserId = User.GetUserId(), CreatedByUserName = User.Identity?.Name, PageUrl = $"/Order/Details?id={source.OrderId}", LinkedOrderPostIds = source.Id.ToString(), ErrorCount = 1 }; _context.EmployeeErrors.Add(error); await _context.SaveChangesAsync(ct); createdErrors.Add(error.Id); }
            _context.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = source.OrderId, CreatedAt = IstanbulTimeHelper.Now, ApplicationUserId = employee.ApplicationUserId, Reason = EmployeeErrorSharePrefix + source.Id, Name = "EmployeeErrorShare" });
        }
        await _context.SaveChangesAsync(ct); return Ok(new { success = true, employeeCount = employees.Count, postIds = createdPosts, employeeErrorIds = createdErrors });
    }

    [HttpGet("/Order/GetEmployeeErrorShareNotifications")]
    public Task<IActionResult> GetEmployeeErrorShareNotifications(CancellationToken ct) => GetHistoryNotifications(EmployeeErrorSharePrefix, ct);

    [HttpPost("/Order/MarkEmployeeErrorShareNotificationRead")]
    public Task<IActionResult> MarkEmployeeErrorShareNotificationRead([FromForm] int id, CancellationToken ct) => MarkHistoryNotificationRead(id, EmployeeErrorSharePrefix, ct);

    [HttpGet("/Order/GetPendingBankTransferFollowUpAdminNotifications")]
    [Authorize(Roles = "Admin,Administrator")]
    public Task<IActionResult> GetPendingBankTransferFollowUpAdminNotifications(CancellationToken ct) => GetHistoryNotifications(BankTransferFollowUpAdminPrefix, ct);

    [HttpPost("/Order/MarkBankTransferFollowUpAdminNotificationRead")]
    [Authorize(Roles = "Admin,Administrator")]
    public Task<IActionResult> MarkBankTransferFollowUpAdminNotificationRead([FromForm] int id, CancellationToken ct) => MarkHistoryNotificationRead(id, BankTransferFollowUpAdminPrefix, ct);

    [HttpGet("/Order/EnsureOperationalReminders")]
    [Authorize]
    public async Task<IActionResult> EnsureOperationalReminders(CancellationToken ct)
    {
        var cutoff = IstanbulTimeHelper.Now.AddHours(-24); var orders = await _context.Orders.AsNoTracking().Where(item => !item.IsHidden && item.LastEditedDate < cutoff && !OrderStatusCodes.ClosedStatuses.Contains(item.OrderStatus)).Select(item => item.Id).Take(100).ToListAsync(ct);
        var existing = await _context.OrderStatusHistories.AsNoTracking().Where(item => orders.Contains(item.OrderId ?? 0) && item.Reason != null && item.Reason.StartsWith(OperationalNotificationPrefix) && item.CreatedAt >= cutoff).Select(item => item.OrderId).ToListAsync(ct);
        foreach (var id in orders.Where(id => !existing.Contains(id))) _context.OrderStatusHistories.Add(new OrderStatusHistory { OrderId = id, CreatedAt = IstanbulTimeHelper.Now, ApplicationUserId = User.GetUserId(), Reason = OperationalNotificationPrefix + "StaleOrder", Name = "OperationalReminder" });
        await _context.SaveChangesAsync(ct); return Ok(new { success = true, createdCount = orders.Count - existing.Count });
    }

    [HttpGet("/Order/GetOperationalNotifications")]
    public Task<IActionResult> GetOperationalNotifications(CancellationToken ct) => GetHistoryNotifications(OperationalNotificationPrefix, ct);

    [HttpPost("/Order/MarkOperationalNotificationRead")]
    public Task<IActionResult> MarkOperationalNotificationRead([FromForm] int id, CancellationToken ct) => MarkHistoryNotificationRead(id, OperationalNotificationPrefix, ct);

    private async Task<IActionResult> GetHistoryNotifications(string prefix, CancellationToken ct)
    {
        var userId = User.GetUserId(); var admin = User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector");
        var rows = await _context.OrderStatusHistories.AsNoTracking().Where(item => !item.IsHidden && item.Reason != null && item.Reason.StartsWith(prefix) && (admin || item.ApplicationUserId == userId)).OrderByDescending(item => item.CreatedAt).Take(100).Select(item => new { id = item.Id, orderId = item.OrderId, item.Reason, item.Name, item.CreatedAt }).ToListAsync(ct);
        return Ok(new { success = true, items = rows, count = rows.Count });
    }

    private async Task<IActionResult> MarkHistoryNotificationRead(int id, string prefix, CancellationToken ct)
    {
        var row = await _context.OrderStatusHistories.FirstOrDefaultAsync(item => item.Id == id && item.Reason != null && item.Reason.StartsWith(prefix), ct); if (row is null) return NotFound(new { success = false }); row.IsHidden = true; await _context.SaveChangesAsync(ct); return Ok(new { success = true });
    }

    private async Task<List<ProblemParticipant>> ProblemParticipants(int orderId, CancellationToken ct)
    {
        var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == orderId, ct); if (order is null) return [];
        var historyIds = await _context.OrderStatusHistories.AsNoTracking().Where(item => item.OrderId == orderId && item.ApplicationUserId != null).Select(item => item.ApplicationUserId!).ToListAsync(ct);
        var ids = historyIds.Concat([order.ApplicationUserId, order.Editedby, order.Fixedby]).Where(item => !string.IsNullOrWhiteSpace(item)).Distinct().ToList();
        return await _context.Employees.AsNoTracking().Where(item => item.ApplicationUserId != null && ids.Contains(item.ApplicationUserId)).Select(item => new ProblemParticipant(item.Id, item.Id, item.ApplicationUserId!, item.DisplayName ?? item.Name, item.ImageUrl, item.Country)).ToListAsync(ct);
    }

    private async Task<decimal> ConvertOrderAmountToEgp(decimal amount, int country, CancellationToken ct)
    {
        if (country == 16) return decimal.Round(amount, 2); var rates = await _context.ExchangeRates.AsNoTracking().Where(rate => rate.Country == country || rate.Country == 16).ToListAsync(ct); var source = rates.FirstOrDefault(rate => rate.Country == country)?.SellToUSD ?? 0; var egp = rates.FirstOrDefault(rate => rate.Country == 16)?.BuyToUSD ?? 0; return source > 0 && egp > 0 ? decimal.Round(amount / source * egp, 2) : 0;
    }

    private static List<int> ParseIntList(string? value) => ParseStringList(value).Select(item => int.TryParse(item, out var id) ? id : 0).Where(id => id > 0).Distinct().Take(100).ToList();
    private static List<string> ParseStringList(string? value) { if (string.IsNullOrWhiteSpace(value)) return []; try { return JsonSerializer.Deserialize<List<string>>(value) ?? []; } catch { return value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(); } }
}

public sealed record ProblemParticipant(int id, int employeeId, string applicationUserId, string name, string? imageUrl, string? country);
