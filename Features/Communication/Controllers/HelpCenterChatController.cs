using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communication/chat")]
[Route("HelpCenterChat")]
public class HelpCenterChatController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public HelpCenterChatController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetMessages")]
    public async Task<ActionResult<List<HelpCenterChatMessage>>> GetMessages([FromQuery] string? receiverUserId, CancellationToken ct)
    {
        var currentUserId = User.GetUserId() ?? "system";
        var query = _context.Set<HelpCenterChatMessage>().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(receiverUserId))
        {
            query = query.Where(m => (m.SenderUserId == currentUserId && m.ReceiverUserId == receiverUserId) || (m.SenderUserId == receiverUserId && m.ReceiverUserId == currentUserId));
        }

        var list = await query.OrderByDescending(m => m.SentAt).Take(100).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [HttpPost("SendMessage")]
    public async Task<ActionResult<HelpCenterChatMessage>> SendMessage([FromBody] SendChatMessageRequest request, CancellationToken ct)
    {
        var msg = new HelpCenterChatMessage
        {
            SenderUserId = User.GetUserId() ?? "system",
            ReceiverUserId = request.ReceiverUserId,
            MessageText = request.MessageText,
            AttachmentUrl = request.AttachmentUrl,
            IsRead = false,
            SentAt = DateTime.UtcNow
        };

        await _context.Set<HelpCenterChatMessage>().AddAsync(msg, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(msg);
    }
}

public record SendChatMessageRequest(string? ReceiverUserId, string MessageText, string? AttachmentUrl);
