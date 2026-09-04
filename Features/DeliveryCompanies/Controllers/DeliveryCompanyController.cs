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
[Authorize]
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
    [Authorize(Roles = "Admin,Administrator,Accountant,Observer,ExecutiveDirector,FollowUpDepartment")]
    public async Task<ActionResult<DeliveryCompanyResult>> GetCompanies([FromQuery] int? countryId, CancellationToken ct)
    {
        var result = await _service.ListCompaniesAsync(countryId, ct);
        return Ok(result);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPost]
    [HttpPost("Create")]
    [HttpPost("/DeliveryCompany/Create")]
    public async Task<ActionResult<DeliveryCompanyRecord>> CreateCompany([FromBody] CreateDeliveryCompanyRequest request, CancellationToken ct)
    {
        var userId = User.GetUserId() ?? "system";
        var result = await _service.CreateCompanyAsync(request, userId, ct);
        return CreatedAtAction(nameof(GetCompanies), new { id = result.Id }, result);
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpGet("/DeliveryCompany/Create")]
    public IActionResult Create() => Ok(new { isRepresentative = false });

    [HttpGet("/DeliveryCompany/Edit")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    public async Task<IActionResult> Edit([FromQuery] int? id, CancellationToken ct)
    {
        if (!id.HasValue) return NotFound();
        var company = await _context.DeliveryCompanies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return company is null ? NotFound() : Ok(company);
    }

    [HttpGet("/DeliveryCompany/Details")]
    [HttpPost("/DeliveryCompany/Details")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
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

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPost("/DeliveryCompany/SetIsActive")]
    public async Task<IActionResult> SetIsActive([FromForm] int Id, [FromForm] bool isActive, CancellationToken ct)
    {
        var changed = await _context.DeliveryCompanies.Where(item => item.Id == Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsActive, isActive), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPost("/DeliveryCompany/SetIsShown")]
    public async Task<IActionResult> SetIsShown([FromForm] int Id, [FromForm] bool isShown, CancellationToken ct)
    {
        var changed = await _context.DeliveryCompanies.Where(item => item.Id == Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsShown, isShown), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPost("/DeliveryCompany/HideNewOrders")]
    public async Task<IActionResult> HideNewOrders([FromForm] int Id, [FromForm] bool hideOrders, CancellationToken ct)
    {
        var changed = await _context.DeliveryCompanies.Where(item => item.Id == Id).ExecuteUpdateAsync(update => update.SetProperty(item => item.IsAllOrdersHidden, hideOrders), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true, hideOrders });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPut("{id:int}")]
    [HttpPost("Edit/{id:int}")]
    [HttpPost("/DeliveryCompany/Edit")]
    public async Task<IActionResult> Edit([RouteOrRequest] int id, [FromBody] CreateDeliveryCompanyRequest request, CancellationToken ct)
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

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpDelete("{id:int}")]
    [HttpPost("Delete/{id:int}")]
    [HttpPost("/DeliveryCompany/Delete")]
    public async Task<IActionResult> Delete([RouteOrRequest] int id, CancellationToken ct)
    {
        var company = await _context.DeliveryCompanies.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (company == null) return NotFound("Delivery company not found.");

        company.IsShown = false;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment")]
    [HttpPost("ToggleActive/{id:int}")]
    [HttpPost("/DeliveryCompany/ToggleActive")]
    public async Task<IActionResult> ToggleActive([RouteOrRequest] int id, CancellationToken ct)
    {
        var company = await _context.DeliveryCompanies.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (company == null) return NotFound("Delivery company not found.");

        company.IsActive = !company.IsActive;
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, isActive = company.IsActive });
    }

    [HttpGet("GetDeliveredStatusCompanyCorrectionOptions")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> GetDeliveredStatusCompanyCorrectionOptions(int historyId, CancellationToken ct)
    {
        var history = await _context.OrderStatusHistories.AsNoTracking().Where(item => item.Id == historyId && item.OrderId.HasValue)
            .Select(item => new { item.Id, OrderId = item.OrderId!.Value, item.Status }).FirstOrDefaultAsync(ct);
        if (history is null) return Ok(new { success = false, specialFlow = false, message = "سجل الحالة غير موجود." });
        if (history.Status != Luxira.Api.Features.Orders.Models.OrderStatusCodes.Delivered) return Ok(new { success = true, specialFlow = false });
        var order = await _context.Orders.AsNoTracking().Where(item => item.Id == history.OrderId).Select(item => new
        {
            item.Id, item.Country, item.IsPaid, item.DeliveryCompanyId,
            CurrentCompanyName = item.DeliveryCompany != null ? item.DeliveryCompany.DisplayName ?? item.DeliveryCompany.Name : string.Empty
        }).FirstOrDefaultAsync(ct);
        if (order is null) return Ok(new { success = false, specialFlow = false, message = "الطلب غير موجود." });
        var companies = await _context.DeliveryCompanies.AsNoTracking().Where(company => company.Country == order.Country && company.IsActive && company.IsShown)
            .Select(company => new { company.Id, Name = company.DisplayName ?? company.Name, company.SupportsCashPayment, company.SupportsBankTransferPayment }).ToListAsync(ct);
        var alternatives = companies.Where(company => company.Id != order.DeliveryCompanyId).ToList();
        var specialFlow = companies.Count > 1 && companies.Any(company => company.SupportsCashPayment) && companies.Any(company => company.SupportsBankTransferPayment) && alternatives.Count > 0;
        return Ok(new { success = true, specialFlow, historyId, orderId = order.Id, currentCompanyId = order.DeliveryCompanyId, currentCompanyName = order.CurrentCompanyName, paymentType = order.IsPaid ? "حوالة بنكية" : "كاش", companies = specialFlow ? alternatives : [] });
    }

    [HttpPost("CorrectDeliveredStatusCompany")]
    [Authorize(Roles = "Admin,Administrator")]
    public async Task<IActionResult> CorrectDeliveredStatusCompany([FromForm] int historyId, [FromForm] int targetDeliveryCompanyId, [FromForm] string? note, CancellationToken ct)
    {
        var normalizedNote = NormalizeCorrection(note, 500);
        if (historyId <= 0 || targetDeliveryCompanyId <= 0 || normalizedNote.Length == 0) return BadRequest(new { success = false, message = "بيانات الحالة أو الشركة أو الملاحظة غير صحيحة." });
        var history = await _context.OrderStatusHistories.FirstOrDefaultAsync(item => item.Id == historyId && item.OrderId.HasValue, ct);
        if (history is null || history.Status != Luxira.Api.Features.Orders.Models.OrderStatusCodes.Delivered) return BadRequest(new { success = false, message = "هذا الإجراء متاح فقط لحالة تم التسليم." });
        var order = await _context.Orders.AsNoTracking().FirstOrDefaultAsync(item => item.Id == history.OrderId, ct);
        if (order is null) return NotFound(new { success = false });
        var target = await _context.DeliveryCompanies.AsNoTracking().FirstOrDefaultAsync(company => company.Id == targetDeliveryCompanyId && company.Country == order.Country && company.IsActive && company.IsShown, ct);
        if (target is null || target.Id == order.DeliveryCompanyId) return BadRequest(new { success = false, message = "اختر شركة مختلفة ومتاحة في نفس دولة الطلب." });
        history.Reason = $"DeliveredByCompanyOverride|{target.Id}|{NormalizeCorrection(target.DisplayName ?? target.Name, 140)}|{normalizedNote}";
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, historyId, companyId = target.Id, companyName = target.DisplayName ?? target.Name, note = normalizedNote, title = $"تم التسليم بواسطة شركة {target.DisplayName ?? target.Name}" });
    }

    [HttpPost("RepairInactiveOrders")]
    [Authorize]
    public async Task<IActionResult> RepairInactiveOrders(CancellationToken ct)
    {
        var inactiveIds = await _context.DeliveryCompanies.AsNoTracking().Where(company => !company.IsRepresentative && !company.IsActive).Select(company => company.Id).ToListAsync(ct);
        var orders = await _context.Orders.Where(order => inactiveIds.Contains(order.DeliveryCompanyId) && !Luxira.Api.Features.Orders.Models.OrderStatusCodes.ClosedStatuses.Contains(order.OrderStatus)).ToListAsync(ct);
        var eligible = orders.Count; var reassigned = 0;
        foreach (var order in orders)
        {
            var replacement = await _context.DeliveryCompanies.AsNoTracking().Where(company => company.Id != order.DeliveryCompanyId && company.Country == order.Country && company.IsActive && company.IsShown && (order.IsPaid ? company.SupportsBankTransferPayment : company.SupportsCashPayment))
                .OrderBy(company => company.Id).Select(company => company.Id).FirstOrDefaultAsync(ct);
            if (replacement <= 0) continue;
            order.DeliveryCompanyId = replacement;
            order.DeliveryPrice = await _context.DeliveryCompanyPrices.AsNoTracking().Where(price => price.DeliveryCompanyId == replacement && (price.City == order.State || price.City == null))
                .OrderByDescending(price => price.City == order.State).Select(price => price.Price).FirstOrDefaultAsync(ct);
            order.LastEditedDate = IstanbulTimeHelper.Now; reassigned++;
        }
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, eligibleOrders = eligible, reassignedOrders = reassigned, notReassignedOrders = eligible - reassigned });
    }

    private static string NormalizeCorrection(string? value, int maxLength)
    {
        var result = (value ?? string.Empty).Replace("|", "¦").Replace("\r\n", "\n").Replace("\r", "\n").Trim();
        return result.Length > maxLength ? result[..maxLength] : result;
    }
}
