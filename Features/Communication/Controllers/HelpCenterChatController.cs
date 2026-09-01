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
        var list = await _context.Set<HelpCenterChatMessage>()
            .AsNoTracking()
            .Where(message => !message.IsDeleted)
            .OrderByDescending(message => message.CreatedAt)
            .Take(100)
            .ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [HttpPost("SendMessage")]
    public async Task<ActionResult<HelpCenterChatMessage>> SendMessage([FromBody] SendChatMessageRequest request, CancellationToken ct)
    {
        var msg = new HelpCenterChatMessage
        {
            SenderUserId = User.GetUserId() ?? "system",
            MessageText = request.MessageText,
            MessageKind = string.IsNullOrWhiteSpace(request.AttachmentStoragePath) ? "Text" : "Attachment",
            AttachmentStoragePath = request.AttachmentStoragePath,
            AttachmentOriginalName = request.AttachmentOriginalName,
            AttachmentMimeType = request.AttachmentMimeType,
            ClientMessageId = request.ClientMessageId,
            ReplyToMessageId = request.ReplyToMessageId,
            CreatedAt = DateTime.UtcNow
        };

        await _context.Set<HelpCenterChatMessage>().AddAsync(msg, ct);
        await _context.SaveChangesAsync(ct);
        return Ok(msg);
    }
}

public sealed record SendChatMessageRequest(
    string MessageText,
    string? AttachmentStoragePath,
    string? AttachmentOriginalName,
    string? AttachmentMimeType,
    string? ClientMessageId,
    int? ReplyToMessageId);
