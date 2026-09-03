using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Orders.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/orders/posts/follow-up")]
[Route("OrderPosts")]
public class OrderPostsFollowUpToolsController : ControllerBase
{
    private const int ProblemType = 0;
    private const int EditNoteType = 1;
    private readonly ApplicationDbContext _context;

    public OrderPostsFollowUpToolsController(ApplicationDbContext context) => _context = context;

    [HttpGet("ListFollowUpEditNotes")]
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> ListFollowUpEditNotes([FromQuery] int orderId, [FromQuery] int type, CancellationToken ct)
    {
        if (orderId <= 0 || type != EditNoteType) return Forbid();
        var currentUserId = User.GetUserId() ?? string.Empty;
        var posts = await (
            from post in _context.OrderPosts.AsNoTracking()
            join employee in _context.Employees.AsNoTracking() on post.AuthorUserId equals employee.ApplicationUserId into employees
            from employee in employees.DefaultIfEmpty()
            join user in _context.Users.AsNoTracking() on post.AuthorUserId equals user.Id into users
            from user in users.DefaultIfEmpty()
            where post.OrderId == orderId && (int)post.Type == EditNoteType
            orderby post.CreatedAt descending
            select new
            {
                id = post.Id,
                orderId = post.OrderId,
                createdAt = (DateTime?)post.CreatedAt,
                authorUserId = post.AuthorUserId,
                authorName = employee != null && employee.DisplayName != null ? employee.DisplayName : user.Name ?? post.AuthorUserId,
                body = post.Body ?? string.Empty,
                images = Array.Empty<object>(),
                isCurrentUser = post.AuthorUserId == currentUserId
            }).ToListAsync(ct);
        return Ok(new { posts, canDelete = true });
    }

    [HttpPost("DeleteEditNoteWithHistory")]
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
    public Task<IActionResult> DeleteEditNoteWithHistory([FromForm] int id, CancellationToken ct) =>
        DeleteWithHistory(id, EditNoteType, "لم يتم العثور على التعديل المطلوب حذفه", ct);

    [HttpGet("DeletedHistory")]
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> DeletedHistory([FromQuery] int orderId, [FromQuery] int type, CancellationToken ct)
    {
        if (orderId <= 0 || type is not (ProblemType or EditNoteType)) return Forbid();
        var items = await _context.OrderPostDeletedHistories.AsNoTracking()
            .Where(item => item.OrderId == orderId && item.Type == type)
            .OrderByDescending(item => item.DeletedAt).ThenByDescending(item => item.Id)
            .ToListAsync(ct);
        return Ok(new { items });
    }

    [HttpPost("DeletePostWithHistory")]
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
    public Task<IActionResult> DeletePostWithHistory([FromForm] int id, [FromForm] int type, CancellationToken ct)
    {
        if (type == ProblemType && !CanSendProblemDeduction()) return Task.FromResult<IActionResult>(Forbid());
        if (type is not (ProblemType or EditNoteType)) return Task.FromResult<IActionResult>(Forbid());
        return DeleteWithHistory(id, type, "لم يتم العثور على الإبلاغ المطلوب حذفه", ct);
    }

    [HttpGet("ProblemDeductionInfo")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> ProblemDeductionInfo([FromQuery] int orderId, CancellationToken ct)
    {
        if (orderId <= 0) return Forbid();
        var order = await _context.Orders.AsNoTracking()
            .Where(item => item.Id == orderId)
            .Select(item => new { item.TotalPrice, item.ApplicationUserId })
            .SingleOrDefaultAsync(ct);
        if (order is null) return NotFound(new { success = false, message = "لم يتم العثور على الطلب" });

        var relatedUserIds = await _context.OrderPosts.AsNoTracking().Where(post => post.OrderId == orderId)
            .Select(post => post.AuthorUserId).Distinct().ToListAsync(ct);
        if (!string.IsNullOrWhiteSpace(order.ApplicationUserId)) relatedUserIds.Add(order.ApplicationUserId);
        var employees = await _context.Employees.AsNoTracking()
            .Where(employee => employee.ApplicationUserId != null && relatedUserIds.Contains(employee.ApplicationUserId))
            .OrderBy(employee => employee.DisplayName ?? employee.Name)
            .Select(employee => new { id = employee.Id, name = employee.DisplayName ?? employee.Name })
            .ToListAsync(ct);
        return Ok(new { success = true, orderTotal = order.TotalPrice, employees });
    }

    [HttpPost("CreateProblemDeduction")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> CreateProblemDeduction(
        [FromForm] int orderId, [FromForm] int employeeId, [FromForm] decimal amount,
        [FromForm] string? reason, [FromForm] string? problemText, CancellationToken ct)
    {
        if (orderId <= 0 || employeeId <= 0) return Forbid();
        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        reason = reason?.Trim();
        problemText = problemText?.Trim();
        if (amount <= 0) return BadRequest(new { success = false, message = "قيمة الخصم غير صحيحة" });
        if (string.IsNullOrWhiteSpace(reason)) return BadRequest(new { success = false, message = "تفاصيل الخصم مطلوبة" });

        var orderTotal = await _context.Orders.AsNoTracking().Where(order => order.Id == orderId)
            .Select(order => (decimal?)order.TotalPrice).SingleOrDefaultAsync(ct);
        if (!orderTotal.HasValue) return NotFound(new { success = false, message = "لم يتم العثور على الطلب" });
        var employeeName = await _context.Employees.AsNoTracking().Where(employee => employee.Id == employeeId)
            .Select(employee => employee.DisplayName ?? employee.Name).SingleOrDefaultAsync(ct);
        if (employeeName is null) return NotFound(new { success = false, message = "لم يتم العثور على الموظف" });

        var now = IstanbulTimeHelper.Now;
        var createdByUserId = User.GetUserId() ?? string.Empty;
        var createdByName = await GetCurrentUserDisplayNameAsync(ct);
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var employeeTransaction = new EmployeeTransaction
        {
            EmployeeId = employeeId,
            Amount = amount,
            TransactionType = EmployeeTransactionType.Deduction,
            Reason = $"خصم بسبب التبليغ عن مشكلة على الطلب #{orderId} - {reason}",
            Date = now
        };
        _context.EmployeeTransactions.Add(employeeTransaction);
        await _context.SaveChangesAsync(ct);
        _context.OrderPostEmployeeDeductions.Add(new OrderPostEmployeeDeduction
        {
            OrderId = orderId,
            EmployeeId = employeeId,
            EmployeeName = employeeName,
            Amount = amount,
            OrderTotal = orderTotal.Value,
            Reason = reason,
            ProblemText = problemText,
            CreatedAt = now,
            CreatedByUserId = createdByUserId,
            CreatedByName = createdByName,
            EmployeeTransactionId = employeeTransaction.Id
        });
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true });
    }

    [HttpGet("ProblemDeductionHistory")]
    [Authorize(Roles = "Admin,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> ProblemDeductionHistory([FromQuery] int orderId, CancellationToken ct)
    {
        if (orderId <= 0) return Forbid();
        var deductions = await _context.OrderPostEmployeeDeductions.AsNoTracking()
            .Where(item => item.OrderId == orderId).OrderByDescending(item => item.CreatedAt)
            .Select(item => new
            {
                kind = "deduction",
                item.Id,
                item.OrderId,
                item.EmployeeId,
                item.EmployeeName,
                item.Amount,
                item.OrderTotal,
                item.Reason,
                item.ProblemText,
                item.CreatedAt,
                item.CreatedByUserId,
                item.CreatedByName,
                item.EmployeeTransactionId
            }).ToListAsync(ct);
        var deleted = await _context.OrderPostDeletedHistories.AsNoTracking()
            .Where(item => item.OrderId == orderId && item.Type == ProblemType)
            .OrderByDescending(item => item.DeletedAt).ToListAsync(ct);
        return Ok(new { items = deductions.Cast<object>().Concat(deleted.Select(item => (object)new { kind = "deleted", item.Id, item.OrderPostId, item.OrderId, item.Type, item.Body, item.AuthorUserId, item.AuthorName, item.CreatedAt, item.DeletedAt, item.DeletedByUserId, item.DeletedByName })) });
    }

    private async Task<IActionResult> DeleteWithHistory(int id, int type, string notFoundMessage, CancellationToken ct)
    {
        if (id <= 0) return Forbid();
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        var post = await _context.OrderPosts.SingleOrDefaultAsync(item => item.Id == id && (int)item.Type == type, ct);
        if (post is null) return NotFound(new { success = false, message = notFoundMessage });
        var authorName = await ResolveUserNameAsync(post.AuthorUserId, ct);
        _context.OrderPostDeletedHistories.Add(new OrderPostDeletedHistory
        {
            OrderPostId = post.Id,
            OrderId = post.OrderId,
            Type = type,
            Body = post.Body,
            AuthorUserId = post.AuthorUserId,
            AuthorName = authorName,
            CreatedAt = post.CreatedAt,
            DeletedAt = IstanbulTimeHelper.Now,
            DeletedByUserId = User.GetUserId(),
            DeletedByName = await GetCurrentUserDisplayNameAsync(ct)
        });
        _context.OrderPosts.Remove(post);
        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true });
    }

    private bool CanSendProblemDeduction() => User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector");

    private async Task<string> GetCurrentUserDisplayNameAsync(CancellationToken ct) =>
        await ResolveUserNameAsync(User.GetUserId() ?? string.Empty, ct);

    private async Task<string> ResolveUserNameAsync(string userId, CancellationToken ct)
    {
        var employeeName = await _context.Employees.AsNoTracking().Where(employee => employee.ApplicationUserId == userId)
            .Select(employee => employee.DisplayName ?? employee.Name).FirstOrDefaultAsync(ct);
        if (!string.IsNullOrWhiteSpace(employeeName)) return employeeName;
        return await _context.Users.AsNoTracking().Where(user => user.Id == userId)
            .Select(user => user.Name ?? user.UserName ?? user.Email).FirstOrDefaultAsync(ct)
            ?? User.Identity?.Name ?? userId;
    }
}
