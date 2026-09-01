using System.Security.Claims;
using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/salaries")]
[Route("api/[controller]")]
public class SalaryController : ControllerBase
{
    private readonly EmployeeService _service;

    public SalaryController(EmployeeService service)
    {
        _service = service;
    }

    [HttpPost("pay")]
    [HttpPost("/EmployeeSalaries/Pay")]
    public async Task<ActionResult<SalaryPaymentDto>> PaySalary([FromBody] RecordSalaryPaymentRequest request, CancellationToken ct)
    {
        var userId = Luxira.Api.Utils.Extensions.ClaimsPrincipalExtensions.GetUserId(User) ?? "system";
        var result = await _service.RecordSalaryPaymentAsync(request, userId, ct);
        return Ok(result);
    }

    [HttpGet("payments")]
    [HttpGet("/EmployeeSalaries/GetPayments")]
    public async Task<ActionResult<List<SalaryPaymentDto>>> GetPayments([FromQuery] int? employeeId, CancellationToken ct)
    {
        var result = await _service.GetSalaryPaymentsAsync(employeeId, ct);
        return Ok(result);
    }
}
