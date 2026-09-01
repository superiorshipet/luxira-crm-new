using Luxira.Api.Data;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/access")]
[Route("EmployeeAccess")]
public class EmployeeAccessController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployeeAccessController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("save-face-print")]
    [HttpPost("/EmployeeAccess/SaveMyFacePrint")]
    public async Task<IActionResult> SaveMyFacePrint([FromBody] SaveFacePrintRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.FaceDescriptor))
        {
            throw new BadRequestException("Face descriptor is required.");
        }

        var userId = User.GetUserId();
        var employee = await _context.Employees.FirstOrDefaultAsync(e => e.ApplicationUserId == userId, ct);
        if (employee == null)
        {
            throw new NotFoundException("Employee record not found for the authenticated user.");
        }

        employee.FaceDescriptor = request.FaceDescriptor.Trim();
        employee.HasFacePrint = true;
        await _context.SaveChangesAsync(ct);

        return Ok(new { success = true, message = "تم حفظ بصمة الوجه بنجاح!" });
    }
}

public record SaveFacePrintRequest(string FaceDescriptor);
