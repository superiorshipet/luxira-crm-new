using Luxira.Api.Data;
using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Features.Employees.Services;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Features.Auth.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees")]
[Route("Employee")]
[Route("api/[controller]")]
public class EmployeeController : ControllerBase
{
    private readonly EmployeeService _service;
    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _storage;

    public EmployeeController(EmployeeService service, ApplicationDbContext context, S3StorageService storage)
    {
        _service = service;
        _context = context;
        _storage = storage;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/Employee/Index")]
    [HttpGet("/Employee/GetEmployees")]
    [HttpPost("/Employee/Index")]
    public async Task<ActionResult<List<EmployeeDto>>> GetEmployees([FromQuery] bool? isActive, CancellationToken ct)
    {
        var result = await _service.GetEmployeesAsync(isActive, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HttpGet("/Employee/GetEmployeeById/{id:int}")]
    [HttpGet("/Employee/Details/{id:int}")]
    public async Task<ActionResult<EmployeeDto>> GetEmployeeById(int id, CancellationToken ct)
    {
        var result = await _service.GetEmployeeByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [HttpPost("Create")]
    [HttpPost("/Employee/Create")]
    [Authorize]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var result = await _service.CreateEmployeeAsync(request, ct);
        return CreatedAtAction(nameof(GetEmployeeById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HttpPost("Edit/{id:int}")]
    [HttpPost("/Employee/Edit/{id:int}")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(int id, [FromBody] UpdateEmployeeRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateEmployeeAsync(id, request, ct);
        return Ok(result);
    }

    [HttpGet("stores")]
    [HttpGet("/Employee/EmployeeStores")]
    [HttpPost("/Employee/EmployeeStores")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> EmployeeStores(CancellationToken ct)
    {
        var stores = await _context.ManufacturingCompanies.Where(m => m.IsShown).Select(m => new { m.Id, m.Name }).ToListAsync(ct);
        return Ok(stores);
    }

    [HttpGet("{id:int}/basic-modal")]
    [HttpGet("/Employee/GetEmployeeBasicModalData")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> GetEmployeeBasicModalData([RouteOrRequest] int id, [FromQuery] int? employeeId, CancellationToken ct)
    {
        var targetId = id > 0 ? id : (employeeId ?? 0);
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == targetId, ct);
        if (employee == null) return NotFound("Employee not found.");

        return Ok(new
        {
            employee.Id,
            employee.Name,
            employee.PhoneNumber,
            employee.Salary,
            employee.JobTitle,
            employee.IsActive
        });
    }

    [HttpPost("update-basic-modal")]
    [HttpPost("/Employee/UpdateEmployeeBasicModal")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> UpdateEmployeeBasicModal([FromBody] UpdateEmployeeBasicModalRequest request, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (employee == null) return NotFound("Employee not found.");

        employee.Name = request.Name;
        employee.PhoneNumber = request.PhoneNumber;
        employee.Salary = request.Salary;
        employee.JobTitle = request.JobTitle;

        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, employee.Id });
    }

    [HttpGet("{id:int}/permissions-modal")]
    [HttpGet("/Employee/GetEmployeePermissionsModalData")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> GetEmployeePermissionsModalData([RouteOrRequest] int id, [FromQuery] int? employeeId, CancellationToken ct)
    {
        var targetId = id > 0 ? id : (employeeId ?? 0);
        var employee = await _context.Employees
            .Include(e => e.ApplicationUser)
            .FirstOrDefaultAsync(e => e.Id == targetId, ct);

        if (employee == null) return NotFound("Employee not found.");

        return Ok(new
        {
            employee.Id,
            employee.Name,
            UserId = employee.ApplicationUserId,
            UserName = employee.ApplicationUser?.UserName,
            employee.IsShown,
            employee.AllowMobileOrTabletLogin,
            employee.ApplyShiftAccess,
            employee.AllowScreenRecording,
            employee.IsNotificationCenterBlocked,
            employee.CanHandleUrgentReports,
            employee.EnableOrderPackaging,
            employee.OrderPackagingNotificationTime,
            employee.OrderPackagingDeliveryCompanyIds,
            employee.OrderPackagingStartGraceMinutes
        });
    }

    [HttpPost("update-permissions-modal")]
    [HttpPost("/Employee/UpdateEmployeePermissionsModal")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> UpdateEmployeePermissionsModal(
        [FromBody] UpdateEmployeePermissionsRequest request,
        CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(item => item.Id == request.Id, ct);
        if (employee is null) throw new NotFoundException("Employee not found.");

        var deliveryCompanyIds = (request.OrderPackagingDeliveryCompanyIds ?? [])
            .Where(id => id > 0).Distinct().OrderBy(id => id).ToList();
        if (request.EnableOrderPackaging)
        {
            if (!request.OrderPackagingNotificationTime.HasValue || deliveryCompanyIds.Count == 0)
                throw new BadRequestException("Packaging time and at least one delivery company are required.");
            var validCount = await _context.DeliveryCompanies.AsNoTracking().CountAsync(
                company => deliveryCompanyIds.Contains(company.Id) && company.IsActive && company.Country == 7,
                ct);
            if (validCount != deliveryCompanyIds.Count)
                throw new BadRequestException("Every packaging delivery company must be an active Turkey company.");
        }

        employee.AllowScreenRecording = request.AllowScreenRecording;
        employee.IsNotificationCenterBlocked = request.IsNotificationCenterBlocked;
        employee.AllowMobileOrTabletLogin = request.AllowMobileOrTabletLogin;
        employee.CanHandleUrgentReports = request.CanHandleUrgentReports;
        employee.ApplyShiftAccess = request.ApplyShiftAccess;
        employee.EnableOrderPackaging = request.EnableOrderPackaging;
        employee.OrderPackagingNotificationTime = request.EnableOrderPackaging
            ? request.OrderPackagingNotificationTime
            : null;
        employee.OrderPackagingDeliveryCompanyIds = request.EnableOrderPackaging
            ? string.Join(',', deliveryCompanyIds)
            : null;
        employee.OrderPackagingDeliveryCompanyId = request.EnableOrderPackaging
            ? deliveryCompanyIds.FirstOrDefault()
            : null;
        employee.OrderPackagingStartGraceMinutes = Math.Clamp(request.OrderPackagingStartGraceMinutes, 1, 180);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("set-active")]
    [HttpPost("/Employee/SetIsActive")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> SetIsActive([FromQuery] int id, [FromQuery] bool isActive, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee == null) return NotFound("Employee not found.");

        employee.IsActive = isActive;
        if (!string.IsNullOrWhiteSpace(employee.ApplicationUserId))
        {
            var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == employee.ApplicationUserId, ct);
            if (user is not null)
            {
                user.LockoutEnd = isActive ? null : DateTimeOffset.MaxValue;
                user.SecurityStamp = Guid.NewGuid().ToString();
            }
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id, isActive });
    }

    [HttpPost("set-shown")]
    [HttpPost("/Employee/SetIsShown")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> SetIsShown([FromQuery] int id, [FromQuery] bool isShown, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (employee is null) throw new NotFoundException("Employee not found.");
        employee.IsShown = isShown;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id, isShown });
    }

    [HttpGet("account-status")]
    [HttpGet("/Employee/GetCurrentAccountStatus")]
    public async Task<IActionResult> GetCurrentAccountStatus(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == userId, ct);

        return Ok(new
        {
            hasAccount = employee != null,
            employeeId = employee?.Id,
            name = employee?.Name,
            salary = employee?.Salary ?? 0m
        });
    }

    [HttpGet("packaging-gate-status")]
    [HttpGet("/Employee/GetOrderPackagingGateStatus")]
    public async Task<IActionResult> GetOrderPackagingGateStatus(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var employee = await _context.Employees.AsNoTracking()
            .FirstOrDefaultAsync(item => item.ApplicationUserId == userId, ct);
        var deliveryCompanyIds = ParseIds(employee?.OrderPackagingDeliveryCompanyIds);
        if (employee is null || !employee.IsActive || !employee.EnableOrderPackaging ||
            !employee.OrderPackagingNotificationTime.HasValue || deliveryCompanyIds.Count == 0)
            return Ok(new { enabled = false });

        var now = IstanbulTimeHelper.Now;
        var scheduledAt = now.Date.Add(employee.OrderPackagingNotificationTime.Value);
        var query = _context.Orders.AsNoTracking().Where(order =>
            !order.IsHidden && order.Country == 7 && deliveryCompanyIds.Contains(order.DeliveryCompanyId));
        var newCount = await query.CountAsync(order => order.OrderStatus == OrderStatusCodes.New, ct);
        var preparedCount = await query.CountAsync(order => order.OrderStatus == OrderStatusCodes.Prepared, ct);
        return Ok(new
        {
            enabled = now >= scheduledAt && (newCount > 0 || preparedCount > 0),
            scheduledAt,
            newCount,
            preparedCount,
            deliveryCompanyIds,
            redirectUrl = "/Order/UpdateAllStatuses?orderPackaging=1"
        });
    }

    [HttpPost("allow-mobile-login")]
    [HttpPost("/Employee/SetAllowMobileOrTabletLogin")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> SetAllowMobileOrTabletLogin(
        [FromQuery] int id,
        [FromQuery] bool isAllowed,
        CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (employee is null) throw new NotFoundException("Employee not found.");
        employee.AllowMobileOrTabletLogin = isAllowed;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id, isAllowed });
    }

    [HttpGet("/Employee/GetPendingDownloadsGatePermission")]
    public IActionResult GetPendingDownloadsGatePermission() => Ok(new
    {
        success = true,
        canBypassPendingDownloadsGate = User.IsInRole("Admin") || User.IsInRole("ExecutiveDirector"),
        canEnterPendingDownloadsPage = true,
        pendingDownloadsAccessRestricted = false,
        pendingDownloadsAccessBeforeShiftEndMinutes = 0,
        pendingDownloadsAccessStartsAt = (DateTime?)null,
        pendingDownloadsShiftEndAt = (DateTime?)null,
        pendingDownloadsRequiredCountryIds = Array.Empty<int>(),
        pendingDownloadsRequiredDeliveryCompanyIds = Array.Empty<int>(),
        appliesToAllCountries = true,
        appliesToAllDeliveryCompanies = true
    });

    [HttpGet("/Employee/Create")]
    [Authorize]
    public async Task<IActionResult> Create(CancellationToken ct) => Ok(new
    {
        users = await _context.Users.AsNoTracking()
            .Where(user => !_context.Employees.Any(employee => employee.ApplicationUserId == user.Id && !employee.IsDeleted))
            .OrderBy(user => user.Name ?? user.UserName)
            .Select(user => new { user.Id, Name = user.Name ?? user.UserName, user.Email }).ToListAsync(ct)
    });

    [HttpGet("/Employee/Edit")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> Edit([FromQuery] int id, CancellationToken ct) => Ok(await _service.GetEmployeeByIdAsync(id, ct));

    [HttpPost("/Employee/Edit")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> Edit([FromQuery] int id, [FromBody] UpdateEmployeeRequest request, CancellationToken ct) =>
        Ok(await _service.UpdateEmployeeAsync(id, request, ct));

    [HttpGet("/Employee/Details")]
    [HttpPost("/Employee/Details")]
    public async Task<IActionResult> Details([FromQuery] int id, CancellationToken ct) => Ok(await _service.GetEmployeeByIdAsync(id, ct));

    [HttpPost("/Employee/DeleteEmployeeAccount")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> DeleteEmployeeAccount([FromForm] int id, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, ct);
        if (employee is null) return NotFound(new { success = false, message = "الموظف غير موجود." });
        employee.IsDeleted = true;
        employee.IsActive = false;
        employee.IsShown = false;
        employee.DeletedAt = IstanbulTimeHelper.Now;
        employee.DeletedByUserId = User.GetUserId();
        employee.DeletedByName = User.Identity?.Name;
        if (!string.IsNullOrWhiteSpace(employee.ApplicationUserId))
        {
            var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == employee.ApplicationUserId, ct);
            if (user is not null)
            {
                user.LockoutEnd = DateTimeOffset.MaxValue;
                user.SecurityStamp = Guid.NewGuid().ToString();
            }
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("/Employee/RestoreDeletedEmployee")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> RestoreDeletedEmployee([FromForm] int id, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(item => item.Id == id && item.IsDeleted, ct);
        if (employee is null) return NotFound(new { success = false, message = "الموظف غير موجود في المحذوفات." });
        employee.IsDeleted = false;
        employee.IsActive = true;
        employee.IsShown = true;
        employee.DeletedAt = null;
        employee.DeletedByUserId = null;
        employee.DeletedByName = null;
        if (!string.IsNullOrWhiteSpace(employee.ApplicationUserId))
        {
            var user = await _context.Users.FirstOrDefaultAsync(item => item.Id == employee.ApplicationUserId, ct);
            if (user is not null)
            {
                user.LockoutEnd = null;
                user.SecurityStamp = Guid.NewGuid().ToString();
            }
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("/Employee/RestoreAllDeletedEmployees")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> RestoreAllDeletedEmployees(CancellationToken ct)
    {
        var employees = await _context.Employees.Where(item => item.IsDeleted).ToListAsync(ct);
        var userIds = employees.Select(item => item.ApplicationUserId).Where(id => id != null).Cast<string>().ToList();
        foreach (var employee in employees)
        {
            employee.IsDeleted = false;
            employee.IsActive = true;
            employee.IsShown = true;
            employee.DeletedAt = null;
            employee.DeletedByUserId = null;
            employee.DeletedByName = null;
        }
        var restoredStamp = Guid.NewGuid().ToString();
        await _context.Users.Where(user => userIds.Contains(user.Id)).ExecuteUpdateAsync(
            setters => setters
                .SetProperty(user => user.LockoutEnd, (DateTimeOffset?)null)
                .SetProperty(user => user.SecurityStamp, restoredStamp),
            ct);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, restoredCount = employees.Count });
    }

    [HttpPost("/Employee/UpdateCurrentProfileImage")]
    [RequestSizeLimit(15 * 1024 * 1024)]
    public async Task<IActionResult> UpdateCurrentProfileImage([FromForm] IFormFile? profileImageFile, [FromForm] string? profileImageBase64, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(item => item.ApplicationUserId == User.GetUserId() && !item.IsDeleted, ct);
        if (employee is null) return NotFound(new { success = false, message = "الموظف غير موجود." });
        if (profileImageFile is null || profileImageFile.Length == 0 || !(profileImageFile.ContentType?.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ?? false))
            return BadRequest(new { success = false, message = "الصورة غير صالحة." });
        var stored = await _storage.UploadAsync(profileImageFile, "employee-profiles", User.GetUserId(), ct);
        employee.ImageUrl = stored.PublicUrl;
        employee.ImageS3Key = stored.S3Key;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, imageUrl = employee.ImageUrl });
    }

    [HttpPost("/Employee/DeleteCurrentProfileImage")]
    public async Task<IActionResult> DeleteCurrentProfileImage(CancellationToken ct)
    {
        var changed = await _context.Employees.Where(item => item.ApplicationUserId == User.GetUserId() && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.ImageUrl, (string?)null).SetProperty(item => item.ImageS3Key, (string?)null), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    [HttpGet("/Employee/GetSoftwareDevelopers")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> GetSoftwareDevelopers(CancellationToken ct)
    {
        string[] roles = ["SoftwareDeveloper", "MarketingDepartment"];
        var users = await (from employee in _context.Employees.AsNoTracking()
                           join userRole in _context.UserRoles.AsNoTracking() on employee.ApplicationUserId equals userRole.UserId
                           join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                           where !employee.IsDeleted && employee.IsActive && role.Name != null && roles.Contains(role.Name)
                           select new { employee.Id, UserId = employee.ApplicationUserId, Name = employee.DisplayName ?? employee.Name, employee.ImageUrl, Role = role.Name })
            .Distinct().OrderBy(item => item.Name).ToListAsync(ct);
        return Ok(new { success = true, items = users });
    }

    [HttpPost("/Employee/AssignDevelopmentTask")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> AssignDevelopmentTask([FromForm] int taskId, [FromForm] int employeeId, CancellationToken ct)
    {
        var employee = await _context.Employees.AsNoTracking().FirstOrDefaultAsync(item => item.Id == employeeId && !item.IsDeleted && item.IsActive, ct);
        if (employee is null || string.IsNullOrWhiteSpace(employee.ApplicationUserId) || !await _context.EmployeeTasks.AnyAsync(task => task.Id == taskId, ct))
            return BadRequest(new { success = false, message = "المهمة أو الموظف غير صحيح." });
        var assignment = await _context.EmployeeTaskAssignments.FirstOrDefaultAsync(item => item.EmployeeTaskId == taskId, ct);
        if (assignment is null)
        {
            assignment = new EmployeeTaskAssignment { EmployeeTaskId = taskId };
            _context.EmployeeTaskAssignments.Add(assignment);
        }
        assignment.EmployeeUserId = employee.ApplicationUserId;
        assignment.EmployeeName = employee.DisplayName ?? employee.Name;
        assignment.EmployeeImageUrl = employee.ImageUrl;
        assignment.AssignedAt = IstanbulTimeHelper.Now;
        assignment.Status = "New";
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, assignment.Id });
    }

    [HttpPost("/Employee/UnassignDevelopmentTask")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> UnassignDevelopmentTask([FromForm] int taskId, CancellationToken ct)
    {
        var count = await _context.EmployeeTaskAssignments.Where(item => item.EmployeeTaskId == taskId).ExecuteDeleteAsync(ct);
        return Ok(new { success = true, deletedCount = count });
    }

    [HttpGet("/Employee/GetDevelopmentTaskAssignments")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> GetDevelopmentTaskAssignments(CancellationToken ct) => Ok(new
    {
        success = true,
        items = await _context.EmployeeTaskAssignments.AsNoTracking().OrderByDescending(item => item.AssignedAt).ToListAsync(ct)
    });

    [HttpGet("/Employee/GetDevelopmentTaskCategoryAssignmentRules")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> GetDevelopmentTaskCategoryAssignmentRules(CancellationToken ct) => Ok(new
    {
        success = true,
        rules = await _context.DevelopmentTaskCategoryAssignmentRules.AsNoTracking()
            .OrderBy(rule => rule.Category)
            .Select(rule => new { category = rule.Category, employeeId = rule.EmployeeId, employeeName = rule.EmployeeName, updatedAt = rule.UpdatedAt })
            .ToListAsync(ct)
    });

    [HttpPost("/Employee/SetDevelopmentTaskCategoryAssignmentRule")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> SetDevelopmentTaskCategoryAssignmentRule([FromForm] int category, [FromForm] int employeeId, CancellationToken ct)
    {
        if (category is not (1 or 2 or 3 or 6))
            return BadRequest(new { success = false, message = "اختاري تصنيف مهام صحيح." });
        if (employeeId <= 0)
        {
            await _context.DevelopmentTaskCategoryAssignmentRules.Where(rule => rule.Category == category).ExecuteDeleteAsync(ct);
            return Ok(new { success = true, category, employeeId = 0, employeeName = string.Empty, affectedTasks = 0, message = "تم إلغاء إلزام التصنيف بأي موظف." });
        }

        var employee = await _context.Employees.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == employeeId, ct);
        if (employee is null || string.IsNullOrWhiteSpace(employee.ApplicationUserId))
            return NotFound(new { success = false, message = "لم يتم العثور على الموظف المختار." });
        var validRole = await (from userRole in _context.UserRoles.AsNoTracking()
                               join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                               where userRole.UserId == employee.ApplicationUserId
                                   && (role.Name == "SoftwareDeveloper" || role.Name == "MarketingDepartment")
                               select userRole.UserId).AnyAsync(ct);
        if (!validRole)
            return BadRequest(new { success = false, message = "الموظف المختار يجب أن تكون صلاحيته مطور برمجيات أو قسم التسويق." });
        var userActive = await _context.Users.AsNoTracking().AnyAsync(user => user.Id == employee.ApplicationUserId
            && user.EmailConfirmed
            && (!user.LockoutEnd.HasValue || user.LockoutEnd <= DateTimeOffset.UtcNow), ct);
        if (employee.IsDeleted || !employee.IsActive || !userActive)
            return BadRequest(new { success = false, message = "حساب الموظف المختار غير نشط." });

        var employeeName = string.IsNullOrWhiteSpace(employee.DisplayName) ? employee.Name.Trim() : employee.DisplayName.Trim();
        var now = DateTimeOffset.UtcNow;
        var userId = User.GetUserId();
        var userName = User.Identity?.Name;
        var rule = await _context.DevelopmentTaskCategoryAssignmentRules.FirstOrDefaultAsync(item => item.Category == category, ct);
        if (rule is null)
        {
            rule = new DevelopmentTaskCategoryAssignmentRule { Category = category };
            _context.DevelopmentTaskCategoryAssignmentRules.Add(rule);
        }
        rule.EmployeeId = employeeId;
        rule.EmployeeName = employeeName;
        rule.UpdatedByUserId = userId;
        rule.UpdatedByName = userName;
        rule.UpdatedAt = now;
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await _context.SaveChangesAsync(ct);

        var affectedTasks = await _context.Database.ExecuteSqlInterpolatedAsync($@"
MERGE dbo.DevelopmentTaskAssignments AS target
USING (SELECT Id AS TaskId FROM dbo.SystemDevelopmentTasks WHERE Category = {category} AND IsDeleted = 0 AND IsCompleted = 0) AS source
ON target.TaskId = source.TaskId
WHEN MATCHED THEN UPDATE SET EmployeeId = {employeeId}, EmployeeName = {employeeName}, AssignedAt = {now}, AssignedByUserId = {userId}, AssignedByName = {userName}, DeveloperStatus = 0, StartedAt = NULL, TimerStartedManually = 0, CompletedAt = NULL
WHEN NOT MATCHED THEN INSERT (TaskId, EmployeeId, EmployeeName, AssignedAt, AssignedByUserId, AssignedByName, DeveloperStatus, StartedAt, TimerStartedManually, CompletedAt)
VALUES (source.TaskId, {employeeId}, {employeeName}, {now}, {userId}, {userName}, 0, NULL, 0, NULL);", ct);
        await transaction.CommitAsync(ct);
        return Ok(new { success = true, category, employeeId, employeeName, affectedTasks, message = "تم إلزام التصنيف بالموظف المختار وتحديث المهام الحالية." });
    }

    private static List<int> ParseIds(string? value) => (value ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(item => int.TryParse(item, out var id) ? id : 0)
        .Where(id => id > 0)
        .Distinct()
        .ToList();
}

public record UpdateEmployeeBasicModalRequest(int Id, string Name, string PhoneNumber, decimal Salary, string? JobTitle);
public record UpdateEmployeePermissionsRequest(
    int Id,
    bool ApplyShiftAccess,
    bool? AllowScreenRecording,
    bool IsNotificationCenterBlocked,
    bool AllowMobileOrTabletLogin,
    bool CanHandleUrgentReports,
    bool EnableOrderPackaging,
    TimeSpan? OrderPackagingNotificationTime,
    List<int>? OrderPackagingDeliveryCompanyIds,
    int OrderPackagingStartGraceMinutes = 20);
