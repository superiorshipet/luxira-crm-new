using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
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

    public WhatsAppDashboardController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetMessages")]
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
    public async Task<ActionResult<WhatsAppMessage>> SendWhatsApp([FromBody] SendWhatsAppRequest request, CancellationToken ct)
    {
        var msg = new WhatsAppMessage
        {
            PhoneNumber = request.PhoneNumber,
            Message = request.Message,
            Direction = "Outbound",
            Status = "Sent",
            OrderId = request.OrderId,
            Timestamp = DateTime.UtcNow
        };

        await _context.Set<WhatsAppMessage>().AddAsync(msg, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(msg);
    }
}

public record SendWhatsAppRequest(string PhoneNumber, string Message, int? OrderId);
