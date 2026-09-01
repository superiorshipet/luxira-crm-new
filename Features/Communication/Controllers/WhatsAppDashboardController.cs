using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Infrastructure.WhatsApp;
using Luxira.Api.Utils.Time;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communication/whatsapp")]
[Route("WhatsAppDashboard")]
public class WhatsAppDashboardController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly LavvaWhatsAppService _lavvaService;
    private readonly WhatsAppAutomationService _automationService;

    public WhatsAppDashboardController(
        ApplicationDbContext context,
        LavvaWhatsAppService lavvaService,
        WhatsAppAutomationService automationService)
    {
        _context = context;
        _lavvaService = lavvaService;
        _automationService = automationService;
    }

    [HttpGet]
    [HttpGet("Index")]
    [HttpGet("/WhatsAppDashboard/Index")]
    [HttpGet("GetMessages")]
    [HttpGet("/WhatsAppDashboard/GetMessages")]
    public async Task<ActionResult<List<WhatsAppMessage>>> GetMessages([FromQuery] string? phone, CancellationToken ct)
    {
        var query = _context.Set<WhatsAppMessage>().AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(phone))
        {
            query = query.Where(m => m.PhoneNumber.Contains(phone));
        }

        var list = await query.OrderByDescending(m => m.Timestamp).Take(100).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost("send")]
    [HttpPost("SendMessage")]
    [HttpPost("/WhatsAppDashboard/SendMessage")]
    public async Task<ActionResult<WhatsAppMessage>> SendWhatsApp([FromBody] SendWhatsAppRequest request, CancellationToken ct)
    {
        var sendResult = await _automationService.SendOrderAlertAsync(request.OrderId ?? 0, request.PhoneNumber, request.Message, ct);

        var msg = new WhatsAppMessage
        {
            PhoneNumber = request.PhoneNumber,
            Message = request.Message,
            Direction = "Outbound",
            Status = sendResult ? "Delivered" : "Sent",
            OrderId = request.OrderId,
            Timestamp = IstanbulTimeHelper.Now
        };

        await _context.Set<WhatsAppMessage>().AddAsync(msg, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(msg);
    }

    [HttpPost("send-failed-delivery")]
    [HttpPost("/WhatsAppDashboard/SendFailedDelivery")]
    public async Task<IActionResult> SendFailedDelivery([FromBody] LavvaFailedDeliveryWhatsAppRequest request, CancellationToken ct)
    {
        var result = await _lavvaService.SendFailedDeliveryAsync(request, ct);
        return Ok(result);
    }
}

public record SendWhatsAppRequest(string PhoneNumber, string Message, int? OrderId);
