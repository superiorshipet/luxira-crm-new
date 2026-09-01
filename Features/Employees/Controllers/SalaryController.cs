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

    [HttpGet("payroll")]
    [HttpGet("/EmployeeSalaries/GetPayrollSummary")]
    [HttpGet("/CallCenterPayroll/GetPayroll")]
    public async Task<ActionResult<List<PayrollSummaryDto>>> GetPayrollSummary(
        [FromQuery] int? employeeId,
        [FromQuery] int? year,
        [FromQuery] int? month,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        int y = year ?? now.Year;
        int m = month ?? now.Month;
        var result = await _service.CalculatePayrollAsync(employeeId, y, m, ct);
        return Ok(result);
    }

    [HttpPost("pay")]
    [HttpPost("/EmployeeSalaries/Pay")]
    [HttpPost("/EmployeeSalaries/ConfirmSalaryPayment")]
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
