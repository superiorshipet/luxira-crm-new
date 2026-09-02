using Luxira.Api.Data;
using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Services;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
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

    public EmployeeController(EmployeeService service, ApplicationDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/Employee/Index")]
    [HttpGet("/Employee/GetEmployees")]
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
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
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
    public async Task<IActionResult> EmployeeStores(CancellationToken ct)
    {
        var stores = await _context.ManufacturingCompanies.Where(m => m.IsShown).Select(m => new { m.Id, m.Name }).ToListAsync(ct);
        return Ok(stores);
    }

    [HttpGet("{id:int}/basic-modal")]
    [HttpGet("/Employee/GetEmployeeBasicModalData")]
    public async Task<IActionResult> GetEmployeeBasicModalData([FromRoute] int id, [FromQuery] int? employeeId, CancellationToken ct)
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
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
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
    public async Task<IActionResult> GetEmployeePermissionsModalData([FromRoute] int id, [FromQuery] int? employeeId, CancellationToken ct)
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
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
    public async Task<IActionResult> SetIsActive([FromQuery] int id, [FromQuery] bool isActive, CancellationToken ct)
    {
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (employee == null) return NotFound("Employee not found.");

        employee.IsActive = isActive;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id, isActive });
    }

    [HttpPost("set-shown")]
    [HttpPost("/Employee/SetIsShown")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,Hr")]
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
