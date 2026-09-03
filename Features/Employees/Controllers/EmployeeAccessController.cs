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

    [HttpGet]
    [HttpGet("Index")]
    [HttpPost("Index")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var companies = await _context.ManufacturingCompanies.AsNoTracking()
            .OrderBy(company => company.Name)
            .Select(company => new
            {
                id = company.Id,
                companyName = company.Name,
                employeeCount = _context.EmployeeManufacturingCompanies.Count(access =>
                    access.ManufacturingCompanyId == company.Id
                    && _context.Employees.Any(employee => employee.Id == access.EmployeeId && employee.IsShown))
            }).ToListAsync(ct);
        return Ok(companies);
    }

    [HttpGet("Details")]
    [HttpPost("Details")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> Details([RouteOrRequest] int id, CancellationToken ct)
    {
        var company = await _context.ManufacturingCompanies.AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new { item.Id, CompanyName = item.Name })
            .SingleOrDefaultAsync(ct);
        if (company is null) return NotFound();

        var employees = await (
            from access in _context.EmployeeManufacturingCompanies.AsNoTracking()
            join employee in _context.Employees.AsNoTracking() on access.EmployeeId equals employee.Id
            where access.ManufacturingCompanyId == id && employee.IsShown
            orderby employee.Name
            select new
            {
                employeeId = employee.Id,
                employeeName = employee.DisplayName ?? employee.Name,
                access.CanSeeManufacturingCompany
            }).ToListAsync(ct);
        return Ok(new { company.Id, company.CompanyName, employees });
    }

    [HttpPost("UpdateEmployeeStatusForStores")]
    [Authorize(Roles = "Admin,ExecutiveDirector")]
    public async Task<IActionResult> UpdateEmployeeStatusForStores(
        [FromForm] int companyId,
        [FromForm] int employeeId,
        [FromForm] bool canSeeManufacturingCompany,
        CancellationToken ct)
    {
        var updated = await _context.EmployeeManufacturingCompanies
            .Where(access => access.ManufacturingCompanyId == companyId && access.EmployeeId == employeeId)
            .ExecuteUpdateAsync(update => update.SetProperty(access => access.CanSeeManufacturingCompany, canSeeManufacturingCompany), ct);
        return updated == 0 ? NotFound() : Ok();
    }

    [HttpGet("RegisterMyFace")]
    public async Task<IActionResult> RegisterMyFace(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var employee = await _context.Employees.AsNoTracking()
            .Where(item => item.ApplicationUserId == userId)
            .Select(item => new
            {
                item.Id,
                employeeName = item.DisplayName ?? item.Name,
                item.ImageUrl,
                item.HasFacePrint,
                alreadyRegistered = item.HasFacePrint
            }).SingleOrDefaultAsync(ct);
        return employee is null ? NotFound("لم يتم العثور على بيانات الموظف الخاصة بك.") : Ok(employee);
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
