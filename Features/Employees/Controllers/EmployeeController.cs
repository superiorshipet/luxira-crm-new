using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees")]
[Route("api/[controller]")]
public class EmployeeController : ControllerBase
{
    private readonly EmployeeService _service;

    public EmployeeController(EmployeeService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/Employee/GetEmployees")]
    public async Task<ActionResult<List<EmployeeDto>>> GetEmployees([FromQuery] bool? isActive, CancellationToken ct)
    {
        var result = await _service.GetEmployeesAsync(isActive, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [HttpGet("/Employee/GetEmployeeById/{id:int}")]
    public async Task<ActionResult<EmployeeDto>> GetEmployeeById(int id, CancellationToken ct)
    {
        var result = await _service.GetEmployeeByIdAsync(id, ct);
        return Ok(result);
    }

    [HttpPost]
    [HttpPost("/Employee/Create")]
    public async Task<ActionResult<EmployeeDto>> CreateEmployee([FromBody] CreateEmployeeRequest request, CancellationToken ct)
    {
        var result = await _service.CreateEmployeeAsync(request, ct);
        return CreatedAtAction(nameof(GetEmployeeById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    [HttpPost("/Employee/Edit/{id:int}")]
    public async Task<ActionResult<EmployeeDto>> UpdateEmployee(int id, [FromBody] UpdateEmployeeRequest request, CancellationToken ct)
    {
        var result = await _service.UpdateEmployeeAsync(id, request, ct);
        return Ok(result);
    }
}
