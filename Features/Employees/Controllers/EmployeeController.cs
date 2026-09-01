using Luxira.Api.Data;
using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Services;
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
            UserName = employee.ApplicationUser?.UserName
        });
    }

    [HttpPost("update-permissions-modal")]
    [HttpPost("/Employee/UpdateEmployeePermissionsModal")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public IActionResult UpdateEmployeePermissionsModal([FromBody] object permissions)
    {
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
    public IActionResult GetOrderPackagingGateStatus()
    {
        return Ok(new { isOpen = true, message = "Packaging gate is operational." });
    }

    [HttpPost("allow-mobile-login")]
    [HttpPost("/Employee/SetAllowMobileOrTabletLogin")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public IActionResult SetAllowMobileOrTabletLogin([FromQuery] int id, [FromQuery] bool isAllowed)
    {
        return Ok(new { success = true, id, isAllowed });
    }
}

public record UpdateEmployeeBasicModalRequest(int Id, string Name, string PhoneNumber, decimal Salary, string? JobTitle);
