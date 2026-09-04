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
[Authorize]
[Route("api/v1/delivery-representatives")]
[Route("DeliveryRepresentative")]
[Route("api/[controller]")]
public class DeliveryRepresentativeController : ControllerBase
{
    private readonly DeliveryCompanyService _service;
    private readonly ApplicationDbContext _context;

    public DeliveryRepresentativeController(DeliveryCompanyService service, ApplicationDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/DeliveryRepresentative/Index")]
    [HttpPost("/DeliveryRepresentative/Index")]
    [HttpGet("/DataList/GetDeliveryRepresentatives")]
    [Authorize(Roles = "Admin,Administrator,Accountant,Observer,ExecutiveDirector,FollowUpDepartment")]
    public async Task<ActionResult<DeliveryRepresentativeResult>> GetRepresentatives([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.ListRepresentativesAsync(countryId, ct);
        return Ok(result);
    }

    [HttpGet("/DeliveryRepresentative/Create")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public IActionResult CreateForm() => Ok(new { isRepresentative = true });

    [HttpGet("/DeliveryRepresentative/Edit")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> EditForm([FromQuery] int? id, CancellationToken ct)
    {
        if (!id.HasValue) return NotFound();
        var rep = await _context.DeliveryCompanies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id && item.IsRepresentative, ct);
        return rep is null ? NotFound() : Ok(rep);
    }

    [HttpGet("/DeliveryRepresentative/Details")]
    [HttpPost("/DeliveryRepresentative/Details")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> DetailsLegacy([FromQuery] int? id, CancellationToken ct)
    {
        if (!id.HasValue) return NotFound();
        var rep = await _context.DeliveryCompanies.AsNoTracking().Include(item => item.Prices).FirstOrDefaultAsync(item => item.Id == id && item.IsRepresentative, ct);
        return rep is null ? NotFound() : Ok(rep);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPost("Create")]
    [HttpPost("/DeliveryRepresentative/Create")]
    public async Task<IActionResult> Create([FromBody] CreateDeliveryRepresentativeRequest request, CancellationToken ct)
    {
        var rep = new DeliveryCompany
        {
            Name = request.Name,
            PhoneNumber = request.PhoneNumber,
            Address = request.Address,
            Country = request.Country,
            City = request.City,
            IsRepresentative = true,
            IsShown = true,
            IsActive = true,
            CreatedDate = IstanbulTimeHelper.Now,
            UserId = User.GetUserId() ?? "system"
        };

        await _context.DeliveryCompanies.AddAsync(rep, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(rep);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPost("Edit/{id:int}")]
    [HttpPut("{id:int}")]
    [HttpPost("/DeliveryRepresentative/Edit")]
    public async Task<IActionResult> Edit([RouteOrRequest] int id, [FromBody] CreateDeliveryRepresentativeRequest request, CancellationToken ct)
    {
        var rep = await _context.DeliveryCompanies.FirstOrDefaultAsync(d => d.Id == id && d.IsRepresentative, ct);
        if (rep == null) return NotFound("Delivery representative not found.");

        rep.Name = request.Name;
        rep.PhoneNumber = request.PhoneNumber;
        rep.Address = request.Address;
        rep.Country = request.Country;
        rep.City = request.City ?? rep.City;

        await _context.SaveChangesAsync(ct);
        return Ok(rep);
    }

    [HttpGet("Details/{id:int}")]
    [HttpGet("/DeliveryRepresentative/Details/{id:int}")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> Details([RouteOrRequest] int id, CancellationToken ct)
    {
        var rep = await _context.DeliveryCompanies.FirstOrDefaultAsync(d => d.Id == id && d.IsRepresentative, ct);
        if (rep == null) return NotFound("Delivery representative not found.");
        return Ok(rep);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPost("SetIsActive")]
    [HttpPost("/DeliveryRepresentative/SetIsActive")]
    public async Task<IActionResult> SetIsActive([FromQuery] int deliveryRepresentativeId, [FromQuery] bool isActive, CancellationToken ct)
    {
        var rep = await _context.DeliveryCompanies.FirstOrDefaultAsync(d => d.Id == deliveryRepresentativeId, ct);
        if (rep == null) return NotFound("Delivery representative not found.");

        rep.IsActive = isActive;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id = deliveryRepresentativeId, isActive });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPost("SetIsShown")]
    [HttpPost("/DeliveryRepresentative/SetIsShown")]
    public async Task<IActionResult> SetIsShown([FromQuery] int deliveryRepresentativeId, [FromQuery] bool isShown, CancellationToken ct)
    {
        var rep = await _context.DeliveryCompanies.FirstOrDefaultAsync(d => d.Id == deliveryRepresentativeId, ct);
        if (rep == null) return NotFound("Delivery representative not found.");

        rep.IsShown = isShown;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, id = deliveryRepresentativeId, isShown });
    }
}

public record CreateDeliveryRepresentativeRequest(string Name, string PhoneNumber, string Address, int Country, string? City);
