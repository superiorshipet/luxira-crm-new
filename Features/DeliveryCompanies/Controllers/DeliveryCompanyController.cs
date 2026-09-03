using System.Security.Claims;
using Luxira.Api.Data;
using Luxira.Api.Features.DeliveryCompanies.DTOs;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Features.DeliveryCompanies.Services;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.DeliveryCompanies.Controllers;

[ApiController]
[Route("api/v1/delivery-companies")]
[Route("DeliveryCompany")]
[Route("api/[controller]")]
public class DeliveryCompanyController : ControllerBase
{
    private readonly DeliveryCompanyService _service;
    private readonly ApplicationDbContext _context;

    public DeliveryCompanyController(DeliveryCompanyService service, ApplicationDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/DeliveryCompany/Index")]
    [HttpPost("/DeliveryCompany/Index")]
    [HttpGet("/DataList/GetDeliveryCompanies")]
    public async Task<ActionResult<DeliveryCompanyResult>> GetCompanies([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.ListCompaniesAsync(countryId, ct);
        return Ok(result);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost]
    [HttpPost("Create")]
    [HttpPost("/DeliveryCompany/Create")]
    public async Task<ActionResult<DeliveryCompanyRecord>> CreateCompany([FromBody] CreateDeliveryCompanyRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        var result = await _service.CreateCompanyAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetCompanies), new { id = result.Id }, result);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpGet("/DeliveryCompany/Create")]
    public IActionResult Create() => Ok(new { isRepresentative = false });

    [HttpGet("/DeliveryCompany/Edit")]
    public async Task<IActionResult> Edit([FromQuery] int? id, CancellationToken ct)
    {
        if (!id.HasValue) return NotFound();
        var company = await _context.DeliveryCompanies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpGet("/DeliveryCompany/Details")]
    [HttpPost("/DeliveryCompany/Details")]
    public async Task<IActionResult> Details([FromQuery] int? id, CancellationToken ct)
    {
        if (!id.HasValue) return NotFound();
        var company = await _context.DeliveryCompanies.AsNoTracking()
            .Include(item => item.Prices)
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        if (company is null) return NotFound();
        var warehouseCount = await _context.Warehouses.AsNoTracking().CountAsync(item => item.DeliveryCompanyId == id, ct);
        return Ok(new { company, warehouseCount });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/DeliveryCompany/SetIsActive")]
    public async Task<IActionResult> SetIsActive([FromForm] int Id, [FromForm] bool isActive, CancellationToken ct)
    {
        var changed = await _context.DeliveryCompanies.Where(item => item.Id == Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, isActive), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/DeliveryCompany/SetIsShown")]
    public async Task<IActionResult> SetIsShown([FromForm] int Id, [FromForm] bool isShown, CancellationToken ct)
    {
        var changed = await _context.DeliveryCompanies.Where(item => item.Id == Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsShown, isShown), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("/DeliveryCompany/HideNewOrders")]
    public async Task<IActionResult> HideNewOrders([FromForm] int Id, [FromForm] bool hideOrders, CancellationToken ct)
    {
        var changed = await _context.DeliveryCompanies.Where(item => item.Id == Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsAllOrdersHidden, hideOrders), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true, hideOrders });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPut("{id:int}")]
    [HttpPost("Edit/{id:int}")]
    [HttpPost("/DeliveryCompany/Edit")]
    public async Task<IActionResult> Edit([FromRoute] int id, [FromBody] CreateDeliveryCompanyRequest request, CancellationToken ct)
    {
        var company = await _context.DeliveryCompanies.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (company == null) return NotFound("Delivery company not found.");

        company.Name = request.Name;
        company.Address = request.Address;
        company.PhoneNumber = request.PhoneNumber;
        company.Country = request.Country;
        company.Notes = request.Notes;
        company.IsRepresentative = request.IsRepresentative;

        await _context.SaveChangesAsync(ct);
        return Ok(company);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpDelete("{id:int}")]
    [HttpPost("Delete/{id:int}")]
    [HttpPost("/DeliveryCompany/Delete")]
    public async Task<IActionResult> Delete([FromRoute] int id, CancellationToken ct)
    {
        var company = await _context.DeliveryCompanies.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (company == null) return NotFound("Delivery company not found.");

        company.IsShown = false;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    [HttpPost("ToggleActive/{id:int}")]
    [HttpPost("/DeliveryCompany/ToggleActive")]
    public async Task<IActionResult> ToggleActive([FromRoute] int id, CancellationToken ct)
    {
        var company = await _context.DeliveryCompanies.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (company == null) return NotFound("Delivery company not found.");

        company.IsActive = !company.IsActive;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, isActive = company.IsActive });
    }
}
