using Luxira.Api.Features.Employees.DTOs;
using Luxira.Api.Features.Employees.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/attendance")]
[Route("api/[controller]")]
public class AttendanceController : ControllerBase
{
    private readonly EmployeeService _service;

    public AttendanceController(EmployeeService service)
    {
        _service = service;
    }

    [HttpPost("check-in")]
    [HttpPost("/EmployeeAttendance/CheckIn")]
    public async Task<ActionResult<AttendanceLogDto>> CheckIn([FromBody] CheckInRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _service.CheckInAsync(request, ip, ct);
        return Ok(result);
    }

    [HttpPost("check-out")]
    [HttpPost("/EmployeeAttendance/CheckOut")]
    public async Task<ActionResult<AttendanceLogDto>> CheckOut([FromBody] CheckOutRequest request, CancellationToken ct)
    {
        var result = await _service.CheckOutAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("logs")]
    [HttpGet("/EmployeeAttendance/GetLogs")]
    public async Task<ActionResult<List<AttendanceLogDto>>> GetLogs(
        [FromQuery] int? employeeId,
        [FromQuery] DateTime? date,
        CancellationToken ct)
    {
        var logs = await _service.GetAttendanceLogsAsync(employeeId, date, ct);
        return Ok(logs);
    }
}
