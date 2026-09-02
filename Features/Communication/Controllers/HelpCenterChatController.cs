using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Luxira.Api.Utils.Extensions;
using Luxira.Api.Utils.Time;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Features.Orders.Models;
using Luxira.Api.Features.ReferenceData.Countries;
using Luxira.Api.Infrastructure.S3;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Security.Claims;

namespace Luxira.Api.Features.Communication.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/communication/chat")]
[Route("HelpCenterChat")]
public class HelpCenterChatController : ControllerBase
{
    private const int PresenceOnlineSeconds = 5 * 60;
    private static readonly string[] DefaultKeywordCategories =
    [
        "استفسار عن موعد التوصيل", "متابعة طلب الزبون", "عدم استلام الطلب",
        "تتبع وموقع الشحنة", "شكوى تأخير", "استفسار عن حالة الطلب",
        "استفسار ومتابعة عامة", "طلب غير مكتمل", "عام",
    ];

    private readonly ApplicationDbContext _context;
    private readonly S3StorageService _storage;
    private readonly IHubContext<OrderHub> _hub;
    private readonly ILogger<HelpCenterChatController> _logger;

    public HelpCenterChatController(
        ApplicationDbContext context,
        S3StorageService storage,
        IHubContext<OrderHub> hub,
        ILogger<HelpCenterChatController> logger)
    {
        _context = context;
        _storage = storage;
        _hub = hub;
        _logger = logger;
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
            SenderName = CurrentUserName(),
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
        await _hub.Clients.All.SendAsync("HelpCenterChatMessageCreated", ToMessageDto(msg), ct);
        return Ok(msg);
    }

    [HttpGet("List")]
    public async Task<IActionResult> List(
        [FromQuery] long? beforeId = null,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId()!;
        take = Math.Clamp(take, 1, 100);
        var query = VisibleMessages();
        if (beforeId.HasValue) query = query.Where(message => message.Id < beforeId.Value);

        var page = await query.OrderByDescending(message => message.Id)
            .Take(take + 1)
            .ToListAsync(ct);
        var hasMore = page.Count > take;
        var messages = page.Take(take).OrderBy(message => message.Id).Select(ToMessageDto).ToList();
        var state = await _context.HelpCenterChatReadStates
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.UserId == userId, ct);
        var lastReadMessageId = state?.LastReadMessageId ?? 0;
        var unreadCount = await VisibleMessages()
            .CountAsync(message => message.Id > lastReadMessageId && message.SenderUserId != userId, ct);
        var settings = await ReadSettingsAsync(ct);

        return Ok(new
        {
            ok = true,
            messages,
            hasMore,
            unreadCount,
            lastReadMessageId,
            currentUserId = userId,
            canManageAll = CanManageAll(),
            canManageSettings = CanManageAll(),
            settings.IsMuted,
            settings.IsReadOnly,
        });
    }

    [HttpGet("Search")]
    public async Task<IActionResult> Search(
        [FromQuery] string? q,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        q = q?.Trim();
        if (string.IsNullOrEmpty(q)) return Ok(new { ok = true, messages = Array.Empty<object>() });
        take = Math.Clamp(take, 1, 100);
        var messages = await VisibleMessages()
            .Where(message => message.SenderName.Contains(q) ||
                              (message.MessageText != null && message.MessageText.Contains(q)) ||
                              (message.AttachmentOriginalName != null && message.AttachmentOriginalName.Contains(q)))
            .OrderByDescending(message => message.Id)
            .Take(take)
            .ToListAsync(ct);
        return Ok(new { ok = true, messages = messages.OrderBy(item => item.Id).Select(ToMessageDto) });
    }

    [HttpGet("NewMessages")]
    public async Task<IActionResult> NewMessages(
        [FromQuery] long afterId = 0,
        [FromQuery] int take = 50,
        CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 100);
        var messages = await VisibleMessages()
            .Where(message => message.Id > Math.Max(0, afterId))
            .OrderBy(message => message.Id)
            .Take(take)
            .ToListAsync(ct);
        return Ok(new { ok = true, messages = messages.Select(ToMessageDto) });
    }

    [HttpPost("Send")]
    [RequestSizeLimit(22L * 1024L * 1024L)]
    public async Task<IActionResult> Send(
        [FromForm] string? text,
        [FromForm] IFormFile? attachment,
        [FromForm] long? replyToMessageId,
        [FromForm] string? mentionedUserIds,
        [FromForm] string? clientMessageId,
        CancellationToken ct)
    {
        var userId = User.GetUserId()!;
        text = text?.Trim();
        if (text?.Length > 4000) return BadRequest(new { message = "الرسالة لا يمكن أن تتجاوز 4000 حرف." });
        if (string.IsNullOrWhiteSpace(text) && (attachment is null || attachment.Length == 0))
            return BadRequest(new { message = "اكتبي رسالة أو أضيفي صورة أو تسجيلًا صوتيًا." });

        var settings = await ReadSettingsAsync(ct);
        if (settings.IsReadOnly && !CanManageAll())
            return BadRequest(new { message = "المحادثة مقفولة للقراءة فقط حاليًا." });

        clientMessageId = string.IsNullOrWhiteSpace(clientMessageId) ? null : clientMessageId.Trim()[..Math.Min(100, clientMessageId.Trim().Length)];
        if (clientMessageId is not null)
        {
            var duplicate = await _context.HelpCenterChatMessages.AsNoTracking()
                .FirstOrDefaultAsync(message => message.SenderUserId == userId && message.ClientMessageId == clientMessageId, ct);
            if (duplicate is not null)
                return Ok(new { ok = true, message = ToMessageDto(duplicate), duplicatePrevented = true });
        }

        if (replyToMessageId.HasValue &&
            !await VisibleMessages().AnyAsync(message => message.Id == replyToMessageId.Value, ct))
            replyToMessageId = null;

        string? storagePath = null;
        if (attachment is { Length: > 0 })
        {
            if (attachment.Length > 20L * 1024L * 1024L)
                return BadRequest(new { message = "حجم المرفق أكبر من الحد المسموح." });
            var stored = await _storage.UploadAsync(attachment, "help-center", userId, ct);
            storagePath = stored.S3Key;
        }

        var message = new HelpCenterChatMessage
        {
            SenderUserId = userId,
            SenderName = CurrentUserName(),
            MessageText = text,
            MessageKind = attachment is null ? "Text" : attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ? "Image" : attachment.ContentType.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ? "Audio" : "Attachment",
            AttachmentStoragePath = storagePath,
            AttachmentOriginalName = attachment is null ? null : Path.GetFileName(attachment.FileName),
            AttachmentMimeType = attachment?.ContentType,
            ReplyToMessageId = replyToMessageId,
            ClientMessageId = clientMessageId,
            CreatedAt = IstanbulTimeHelper.Now,
        };
        _context.HelpCenterChatMessages.Add(message);
        await _context.SaveChangesAsync(ct);

        var mentions = (mentionedUserIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.Ordinal)
            .Take(100)
            .Select(id => new HelpCenterChatMention { MessageId = message.Id, MentionedUserId = id })
            .ToList();
        if (mentions.Count > 0)
        {
            _context.HelpCenterChatMentions.AddRange(mentions);
            await _context.SaveChangesAsync(ct);
        }

        var payload = ToMessageDto(message);
        await _hub.Clients.All.SendAsync("HelpCenterChatMessageCreated", payload, ct);
        return Ok(new { ok = true, message = payload });
    }

    [HttpPost("Edit")]
    public async Task<IActionResult> Edit(
        [FromForm] long id,
        [FromForm] string? text,
        CancellationToken ct)
    {
        text = text?.Trim();
        if (string.IsNullOrEmpty(text) || text.Length > 4000)
            return BadRequest(new { message = "لا يمكن حفظ رسالة فارغة أو أطول من 4000 حرف." });
        var message = await _context.HelpCenterChatMessages.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (message is null) return NotFound(new { message = "الرسالة غير موجودة." });
        var canManage = CanManageAll();
        if (!canManage && message.SenderUserId != User.GetUserId()) return Forbid();
        if (!canManage && IstanbulTimeHelper.Now > message.CreatedAt.AddMinutes(5))
            return BadRequest(new { message = "انتهت مدة تعديل الرسالة المحددة بخمس دقائق." });

        if (message.MessageText == text) return Ok(new { ok = true, unchanged = true });
        var now = IstanbulTimeHelper.Now;
        _context.HelpCenterChatMessageEdits.Add(new HelpCenterChatMessageEdit
        {
            MessageId = id,
            EditorUserId = User.GetUserId()!,
            EditorName = CurrentUserName(),
            OldMessageText = message.MessageText,
            NewMessageText = text,
            EditedAt = now,
        });
        message.MessageText = text;
        message.EditedAt = canManage ? null : now;
        await _context.SaveChangesAsync(ct);
        var payload = new { id, text, isEdited = !canManage, editedAt = canManage ? null : now.ToString("yyyy-MM-ddTHH:mm:ss") };
        await _hub.Clients.All.SendAsync("HelpCenterChatMessageUpdated", payload, ct);
        return Ok(new { ok = true, payload });
    }

    [HttpGet("EditHistory")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> EditHistory([FromQuery] long id, CancellationToken ct)
    {
        var items = await _context.HelpCenterChatMessageEdits.AsNoTracking()
            .Where(item => item.MessageId == id)
            .OrderByDescending(item => item.Id)
            .Select(item => new { oldText = item.OldMessageText, newText = item.NewMessageText, editorName = item.EditorName, editedAt = item.EditedAt })
            .ToListAsync(ct);
        return Ok(new { ok = true, items });
    }

    [HttpPost("ToggleReaction")]
    public async Task<IActionResult> ToggleReaction(
        [FromForm] long id,
        [FromForm] string? emoji,
        CancellationToken ct)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "👍", "❤️", "😂", "😮", "😢", "🙏" };
        emoji = emoji?.Trim();
        if (emoji is null || !allowed.Contains(emoji))
            return BadRequest(new { message = "الإيموجي غير مدعوم." });
        if (!await VisibleMessages().AnyAsync(message => message.Id == id, ct))
            return NotFound(new { message = "الرسالة غير موجودة." });

        var userId = User.GetUserId()!;
        var reaction = await _context.HelpCenterChatReactions
            .FirstOrDefaultAsync(item => item.MessageId == id && item.UserId == userId && item.Emoji == emoji, ct);
        var added = reaction is null;
        if (reaction is null)
        {
            _context.HelpCenterChatReactions.Add(new HelpCenterChatReaction
            {
                MessageId = id,
                UserId = userId,
                UserName = CurrentUserName(),
                Emoji = emoji,
                CreatedAt = IstanbulTimeHelper.Now,
            });
        }
        else
        {
            _context.HelpCenterChatReactions.Remove(reaction);
        }
        await _context.SaveChangesAsync(ct);
        var reactions = await _context.HelpCenterChatReactions.AsNoTracking()
            .Where(item => item.MessageId == id)
            .GroupBy(item => item.Emoji)
            .Select(group => new
            {
                emoji = group.Key,
                count = group.Count(),
                reactedByCurrentUser = group.Any(item => item.UserId == userId),
            })
            .ToListAsync(ct);
        var payload = new { id, emoji, added, reactions, reactedByUserId = userId, reactedByName = CurrentUserName() };
        await _hub.Clients.All.SendAsync("HelpCenterChatReactionChanged", payload, ct);
        return Ok(new { ok = true, added, payload });
    }

    [HttpPost("TogglePin")]
    public async Task<IActionResult> TogglePin([FromForm] long id, CancellationToken ct)
    {
        if (!await VisibleMessages().AnyAsync(message => message.Id == id, ct)) return NotFound();
        var userId = User.GetUserId()!;
        var pin = await _context.HelpCenterChatPins
            .FirstOrDefaultAsync(item => item.UserId == userId && item.MessageId == id, ct);
        var isPinned = pin is null;
        if (pin is not null)
        {
            _context.HelpCenterChatPins.Remove(pin);
        }
        else
        {
            var count = await _context.HelpCenterChatPins.CountAsync(item => item.UserId == userId, ct);
            if (count >= 3) return BadRequest(new { message = "يمكن تثبيت ثلاث رسائل فقط." });
            _context.HelpCenterChatPins.Add(new HelpCenterChatPin
            {
                UserId = userId,
                MessageId = id,
                PinnedAt = IstanbulTimeHelper.Now,
            });
        }
        await _context.SaveChangesAsync(ct);
        var payload = new { id, isPinned };
        await _hub.Clients.User(userId).SendAsync("HelpCenterChatPinChanged", payload, ct);
        return Ok(new { ok = true, payload });
    }

    [HttpGet("Pinned")]
    public async Task<IActionResult> Pinned(CancellationToken ct)
    {
        var userId = User.GetUserId()!;
        var messages = await (
            from pin in _context.HelpCenterChatPins.AsNoTracking()
            join message in VisibleMessages() on pin.MessageId equals message.Id
            where pin.UserId == userId
            orderby pin.PinnedAt descending
            select message)
            .ToListAsync(ct);
        return Ok(new { ok = true, messages = messages.Select(ToMessageDto) });
    }

    [HttpPost("Delete")]
    public Task<IActionResult> Delete([FromForm] long id, CancellationToken ct) =>
        DeleteMany(id.ToString(), ct);

    [HttpPost("DeleteMany")]
    public async Task<IActionResult> DeleteMany([FromForm] string? ids, CancellationToken ct)
    {
        var messageIds = (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => long.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .Take(500)
            .ToList();
        if (messageIds.Count == 0) return BadRequest(new { message = "لم يتم تحديد رسائل." });

        var userId = User.GetUserId()!;
        var canManage = CanManageAll();
        var cutoff = IstanbulTimeHelper.Now.AddMinutes(-5);
        var messages = await _context.HelpCenterChatMessages
            .Where(message => messageIds.Contains(message.Id) &&
                              (canManage || (message.SenderUserId == userId && message.CreatedAt >= cutoff)))
            .ToListAsync(ct);
        foreach (var message in messages)
        {
            message.IsDeleted = true;
            message.DeletedAt = IstanbulTimeHelper.Now;
            message.DeletedByUserId = userId;
            message.DeletedByName = CurrentUserName();
        }
        await _context.SaveChangesAsync(ct);
        var deletedIds = messages.Select(message => message.Id).ToList();
        var payload = new { ids = deletedIds };
        await _hub.Clients.All.SendAsync("HelpCenterChatMessagesDeleted", payload, ct);
        return Ok(new { ok = true, payload });
    }

    [HttpPost("MarkRead")]
    public async Task<IActionResult> MarkRead([FromForm] long lastMessageId, CancellationToken ct)
    {
        var userId = User.GetUserId()!;
        var now = IstanbulTimeHelper.Now;
        var state = await _context.HelpCenterChatReadStates.FirstOrDefaultAsync(item => item.UserId == userId, ct);
        if (state is null)
        {
            state = new HelpCenterChatReadState { UserId = userId };
            _context.HelpCenterChatReadStates.Add(state);
        }
        state.LastReadMessageId = Math.Max(state.LastReadMessageId, lastMessageId);
        state.LastReadAt = now;

        var unreadMessageIds = await VisibleMessages()
            .Where(message => message.Id <= lastMessageId && message.SenderUserId != userId)
            .Select(message => message.Id)
            .ToListAsync(ct);
        var existing = await _context.HelpCenterChatMessageReads
            .Where(read => unreadMessageIds.Contains(read.MessageId) && read.UserId == userId)
            .Select(read => read.MessageId)
            .ToListAsync(ct);
        var missing = unreadMessageIds.Except(existing).Select(messageId => new HelpCenterChatMessageRead
        {
            MessageId = messageId,
            UserId = userId,
            UserName = CurrentUserName(),
            ReadAt = now,
        });
        _context.HelpCenterChatMessageReads.AddRange(missing);
        await _context.SaveChangesAsync(ct);
        var payload = new { userId, lastMessageId = state.LastReadMessageId, readAt = now };
        await _hub.Clients.All.SendAsync("HelpCenterChatMessagesRead", payload, ct);
        return Ok(new { ok = true, payload });
    }

    [HttpGet("UnreadCount")]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var userId = User.GetUserId()!;
        var lastRead = await _context.HelpCenterChatReadStates.AsNoTracking()
            .Where(item => item.UserId == userId)
            .Select(item => (long?)item.LastReadMessageId)
            .FirstOrDefaultAsync(ct) ?? 0;
        var count = await VisibleMessages().CountAsync(message => message.Id > lastRead && message.SenderUserId != userId, ct);
        return Ok(new { ok = true, unreadCount = count, lastReadMessageId = lastRead });
    }

    [HttpGet("Readers")]
    public async Task<IActionResult> Readers([FromQuery] long id, CancellationToken ct)
    {
        var message = await _context.HelpCenterChatMessages.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        if (message is null) return NotFound();
        if (!CanManageAll() && message.SenderUserId != User.GetUserId()) return Forbid();
        var readers = await _context.HelpCenterChatMessageReads.AsNoTracking()
            .Where(item => item.MessageId == id)
            .OrderBy(item => item.ReadAt)
            .Select(item => new { userId = item.UserId, userName = item.UserName, userImageUrl = item.UserImageUrl, readAt = item.ReadAt })
            .ToListAsync(ct);
        return Ok(new { ok = true, readers });
    }

    [HttpGet("Settings")]
    public async Task<IActionResult> Settings(CancellationToken ct) =>
        Ok(new { ok = true, settings = await ReadSettingsAsync(ct) });

    [HttpPost("UpdateSettings")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> UpdateSettings(
        [FromForm] bool? isMuted,
        [FromForm] bool? isReadOnly,
        CancellationToken ct)
    {
        var settings = await _context.HelpCenterChatSettings.FirstOrDefaultAsync(item => item.Id == 1, ct);
        if (settings is null)
        {
            settings = new HelpCenterChatSetting { Id = 1 };
            _context.HelpCenterChatSettings.Add(settings);
        }
        if (isMuted.HasValue) settings.IsMuted = isMuted.Value;
        if (isReadOnly.HasValue) settings.IsReadOnly = isReadOnly.Value;
        settings.UpdatedAt = IstanbulTimeHelper.Now;
        settings.UpdatedByUserId = User.GetUserId();
        settings.UpdatedByName = CurrentUserName();
        await _context.SaveChangesAsync(ct);
        var payload = new { settings.IsMuted, settings.IsReadOnly, settings.UpdatedAt };
        await _hub.Clients.All.SendAsync("HelpCenterChatSettingsChanged", payload, ct);
        return Ok(new { ok = true, payload });
    }

    [HttpGet("SearchOrdersForLink")]
    public async Task<IActionResult> SearchOrdersForLink(
        [FromQuery] string? q,
        [FromQuery] int take = 10,
        CancellationToken ct = default)
    {
        q = q?.Trim();
        if (string.IsNullOrEmpty(q)) return Ok(new { ok = true, orders = Array.Empty<object>() });
        take = Math.Clamp(take, 1, 20);
        var exactId = int.TryParse(q, out var parsedId) && parsedId > 0 ? parsedId : (int?)null;
        var orders = await (
            from order in _context.Orders.AsNoTracking()
            join store in _context.ManufacturingCompanies.AsNoTracking()
                on order.ManufacturingCompanyId equals (int?)store.Id into stores
            from store in stores.DefaultIfEmpty()
            where exactId.HasValue
                ? order.Id == exactId.Value
                : order.CustomerName == q || order.TelephoneNumber == q || order.SecondTelephoneNumber == q
            orderby order.Id descending
            select new
            {
                orderId = order.Id,
                customerName = order.CustomerName,
                phone = order.TelephoneNumber,
                secondPhone = order.SecondTelephoneNumber ?? string.Empty,
                storeName = store != null ? store.Name : string.Empty,
                order.Country,
                createdAt = order.CreatedDate,
            })
            .Take(exactId.HasValue ? 1 : take)
            .ToListAsync(ct);
        var result = orders.Select(order => new
        {
            order.orderId,
            order.customerName,
            order.phone,
            order.secondPhone,
            order.storeName,
            countryName = CountryCatalog.All.FirstOrDefault(country => country.Id == order.Country)?.Name ?? order.Country.ToString(),
            countryFlagUrl = CountryCatalog.All.FirstOrDefault(country => country.Id == order.Country)?.ImageUrl ?? string.Empty,
            createdAt = order.createdAt.ToString("yyyy-MM-ddTHH:mm:ss"),
        });
        return Ok(new { ok = true, orders = result });
    }

    [HttpGet("MessageOrderLinks")]
    public async Task<IActionResult> MessageOrderLinks([FromQuery] long messageId, CancellationToken ct)
    {
        if (!await _context.HelpCenterChatMessages.AnyAsync(message => message.Id == messageId, ct)) return NotFound();
        var links = await (
            from link in _context.HelpCenterChatMessageOrderLinks.AsNoTracking()
            join order in _context.Orders.AsNoTracking() on link.OrderId equals order.Id
            join store in _context.ManufacturingCompanies.AsNoTracking()
                on order.ManufacturingCompanyId equals (int?)store.Id into stores
            from store in stores.DefaultIfEmpty()
            where link.MessageId == messageId
            orderby link.LinkedAt descending
            select new
            {
                linkId = link.Id,
                orderId = order.Id,
                customerName = order.CustomerName,
                phone = order.TelephoneNumber,
                storeName = store != null ? store.Name : string.Empty,
                country = order.Country,
                linkedByName = link.LinkedByName,
                linkedAt = link.LinkedAt,
            })
            .ToListAsync(ct);
        return Ok(new
        {
            ok = true,
            links = links.Select(link => new
            {
                link.linkId,
                link.orderId,
                link.customerName,
                link.phone,
                link.storeName,
                countryName = CountryCatalog.All.FirstOrDefault(country => country.Id == link.country)?.Name ?? link.country.ToString(),
                link.linkedByName,
                linkedAt = link.linkedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            }),
        });
    }

    [HttpPost("LinkMessageToOrder")]
    public async Task<IActionResult> LinkMessageToOrder(
        [FromForm] long messageId,
        [FromForm] int orderId,
        CancellationToken ct)
    {
        if (!await _context.HelpCenterChatMessages.AnyAsync(message => message.Id == messageId, ct))
            return NotFound(new { message = "الرسالة غير موجودة." });
        if (!await _context.Orders.AnyAsync(order => order.Id == orderId, ct))
            return NotFound(new { message = "الطلب غير موجود." });
        var exists = await _context.HelpCenterChatMessageOrderLinks
            .AnyAsync(link => link.MessageId == messageId && link.OrderId == orderId, ct);
        var now = IstanbulTimeHelper.Now;
        if (!exists)
        {
            _context.HelpCenterChatMessageOrderLinks.Add(new HelpCenterChatMessageOrderLink
            {
                MessageId = messageId,
                OrderId = orderId,
                LinkedByUserId = User.GetUserId()!,
                LinkedByName = CurrentUserName(),
                LinkedAt = now,
            });
            await _context.SaveChangesAsync(ct);
        }
        var payload = new { messageId, orderId, linkedByName = CurrentUserName(), linkedAt = now.ToString("yyyy-MM-ddTHH:mm:ss") };
        if (!exists) await _hub.Clients.All.SendAsync("HelpCenterChatMessageOrderLinked", payload, ct);
        try
        {
            if (!exists)
                await ApplyQuestionOrderStatusRuleAsync(messageId, orderId, User.GetUserId()!, ct);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Help Center order rule failed for message {MessageId} and order {OrderId}.", messageId, orderId);
        }
        return Ok(new
        {
            ok = true,
            inserted = !exists,
            message = exists ? "الرسالة مرتبطة بهذا الطلب بالفعل." : "تم ربط الرسالة بالطلب.",
            payload,
        });
    }

    [HttpPost("UnlinkMessageFromOrder")]
    public async Task<IActionResult> UnlinkMessageFromOrder(
        [FromForm] long messageId,
        [FromForm] int orderId,
        CancellationToken ct)
    {
        var link = await _context.HelpCenterChatMessageOrderLinks
            .FirstOrDefaultAsync(item => item.MessageId == messageId && item.OrderId == orderId, ct);
        if (link is not null)
        {
            _context.HelpCenterChatMessageOrderLinks.Remove(link);
            await _context.SaveChangesAsync(ct);
        }
        var payload = new { messageId, orderId };
        if (link is not null) await _hub.Clients.All.SendAsync("HelpCenterChatMessageOrderUnlinked", payload, ct);
        return Ok(new { ok = true, removed = link is not null, payload });
    }

    [HttpGet("OrderLinkedMessages")]
    public async Task<IActionResult> OrderLinkedMessages([FromQuery] int orderId, CancellationToken ct)
    {
        var messages = await (
            from link in _context.HelpCenterChatMessageOrderLinks.AsNoTracking()
            join message in VisibleMessages() on link.MessageId equals message.Id
            where link.OrderId == orderId
            orderby link.LinkedAt descending
            select new { link.Id, link.LinkedAt, link.LinkedByName, Message = message })
            .ToListAsync(ct);
        return Ok(new
        {
            ok = true,
            orderId,
            messages = messages.Select(item => new
            {
                linkId = item.Id,
                messageId = item.Message.Id,
                message = ToMessageDto(item.Message),
                item.LinkedByName,
                linkedAt = item.LinkedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            }),
        });
    }

    [HttpPost("DeleteForMe")]
    public async Task<IActionResult> DeleteForMe([FromForm] string? ids, CancellationToken ct)
    {
        var userId = User.GetUserId()!;
        var messageIds = ParseMessageIds(ids);
        var existing = await _context.HelpCenterChatMessageHiddenForUsers
            .Where(item => item.UserId == userId && messageIds.Contains(item.MessageId))
            .Select(item => item.MessageId)
            .ToListAsync(ct);
        _context.HelpCenterChatMessageHiddenForUsers.AddRange(
            messageIds.Except(existing).Select(messageId => new HelpCenterChatMessageHiddenForUser
            {
                MessageId = messageId,
                UserId = userId,
                HiddenAt = IstanbulTimeHelper.Now,
            }));
        await _context.SaveChangesAsync(ct);
        var payload = new { ids = messageIds };
        await _hub.Clients.User(userId).SendAsync("HelpCenterChatMessagesHiddenForMe", payload, ct);
        return Ok(new { ok = true, payload });
    }

    [HttpPost("HardDelete")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector")]
    public async Task<IActionResult> HardDelete([FromForm] long id, CancellationToken ct)
    {
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);
        await _context.HelpCenterChatMessageOrderLinks.Where(item => item.MessageId == id).ExecuteDeleteAsync(ct);
        await _context.HelpCenterChatMessageEdits.Where(item => item.MessageId == id).ExecuteDeleteAsync(ct);
        await _context.HelpCenterChatReactions.Where(item => item.MessageId == id).ExecuteDeleteAsync(ct);
        await _context.HelpCenterChatPins.Where(item => item.MessageId == id).ExecuteDeleteAsync(ct);
        await _context.HelpCenterChatMessageReads.Where(item => item.MessageId == id).ExecuteDeleteAsync(ct);
        await _context.HelpCenterChatMentions.Where(item => item.MessageId == id).ExecuteDeleteAsync(ct);
        await _context.HelpCenterChatMessageHiddenForUsers.Where(item => item.MessageId == id).ExecuteDeleteAsync(ct);
        var deleted = await _context.HelpCenterChatMessages.Where(item => item.Id == id).ExecuteDeleteAsync(ct);
        await transaction.CommitAsync(ct);
        if (deleted == 0) return NotFound();
        var payload = new { ids = new[] { id } };
        await _hub.Clients.All.SendAsync("HelpCenterChatMessagesHardDeleted", payload, ct);
        return Ok(new { ok = true, payload });
    }

    [HttpGet("AroundMessage")]
    public async Task<IActionResult> AroundMessage(
        [FromQuery] long id,
        [FromQuery] int before = 25,
        [FromQuery] int after = 25,
        CancellationToken ct = default)
    {
        before = Math.Clamp(before, 1, 60);
        after = Math.Clamp(after, 1, 60);
        if (!await VisibleMessages().AnyAsync(message => message.Id == id, ct)) return NotFound();
        var previous = await VisibleMessages().Where(message => message.Id <= id)
            .OrderByDescending(message => message.Id).Take(before).ToListAsync(ct);
        var following = await VisibleMessages().Where(message => message.Id > id)
            .OrderBy(message => message.Id).Take(after).ToListAsync(ct);
        var messages = previous.Concat(following).OrderBy(message => message.Id).Select(ToMessageDto);
        return Ok(new { ok = true, messages, focusMessageId = id, hasMoreBefore = previous.Count == before, hasMoreAfter = following.Count == after });
    }

    [HttpGet("Members")]
    public async Task<IActionResult> Members(CancellationToken ct)
    {
        var now = IstanbulTimeHelper.Now;
        var members = await _context.Employees.AsNoTracking()
            .Where(employee => employee.ApplicationUserId != null && employee.ApplicationUserId != "" &&
                               employee.IsShown && employee.IsActive &&
                               _context.Users.Any(user => user.Id == employee.ApplicationUserId && user.EmailConfirmed))
            .OrderBy(employee => employee.DisplayName ?? employee.Name)
            .Select(employee => new
            {
                userId = employee.ApplicationUserId!,
                name = employee.DisplayName ?? employee.Name,
                imageUrl = employee.ImageUrl ?? string.Empty,
            })
            .ToListAsync(ct);

        var mentionMembers = await _context.Employees.AsNoTracking()
            .Where(employee => employee.ApplicationUserId != null && employee.ApplicationUserId != "" &&
                               employee.IsActive &&
                               _context.Users.Any(user => user.Id == employee.ApplicationUserId && user.EmailConfirmed))
            .OrderBy(employee => employee.DisplayName ?? employee.Name)
            .Select(employee => new
            {
                userId = employee.ApplicationUserId!,
                name = employee.DisplayName ?? employee.Name,
                imageUrl = employee.ImageUrl ?? string.Empty,
            })
            .ToListAsync(ct);

        var userIds = members.Select(member => member.userId).ToList();
        var presence = await _context.HelpCenterChatUserPresence.AsNoTracking()
            .Where(item => userIds.Contains(item.UserId))
            .ToDictionaryAsync(item => item.UserId, StringComparer.Ordinal, ct);
        var totalEmployeesCount = await _context.Employees.AsNoTracking()
            .Where(employee => employee.ApplicationUserId != null && employee.ApplicationUserId != "")
            .Select(employee => employee.ApplicationUserId!)
            .Distinct()
            .CountAsync(ct);
        var deliveryCounts = await _context.DeliveryCompanies.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Companies = group.Count(item => !item.IsRepresentative),
                Representatives = group.Count(item => item.IsRepresentative),
            })
            .FirstOrDefaultAsync(ct);
        var deliveryCompaniesCount = deliveryCounts?.Companies ?? 0;
        var deliveryRepresentativesCount = deliveryCounts?.Representatives ?? 0;

        return Ok(new
        {
            ok = true,
            totalMembers = totalEmployeesCount + deliveryCompaniesCount + deliveryRepresentativesCount,
            totalEmployeesCount,
            deliveryCompaniesCount,
            deliveryRepresentativesCount,
            mentionMembers = mentionMembers.DistinctBy(member => member.userId),
            members = members.Select(member =>
            {
                presence.TryGetValue(member.userId, out var state);
                return new
                {
                    member.userId,
                    member.name,
                    member.imageUrl,
                    isOnline = state is not null && now <= state.LastSeenAt.AddSeconds(PresenceOnlineSeconds),
                    lastSeenAt = state?.LastSeenAt.ToString("yyyy-MM-ddTHH:mm:ss"),
                };
            }),
        });
    }

    [HttpPost("Heartbeat")]
    public async Task<IActionResult> Heartbeat(
        [FromForm] bool isOpen = true,
        [FromForm] bool broadcast = false,
        CancellationToken ct = default)
    {
        var userId = User.GetUserId()!;
        var now = IstanbulTimeHelper.Now;
        var employee = await _context.Employees.AsNoTracking()
            .Where(item => item.ApplicationUserId == userId)
            .Select(item => new { Name = item.DisplayName ?? item.Name, item.ImageUrl })
            .FirstOrDefaultAsync(ct);
        var userName = employee?.Name ?? CurrentUserName();
        var imageUrl = employee?.ImageUrl ?? string.Empty;
        var presence = await _context.HelpCenterChatUserPresence
            .FirstOrDefaultAsync(item => item.UserId == userId, ct);
        if (presence is null)
        {
            presence = new HelpCenterChatUserPresence { UserId = userId };
            _context.HelpCenterChatUserPresence.Add(presence);
        }
        presence.UserName = userName;
        presence.UserImageUrl = imageUrl;
        presence.LastSeenAt = now;
        presence.IsChatOpen = isOpen;
        await _context.SaveChangesAsync(ct);

        var payload = new
        {
            userId,
            name = userName,
            imageUrl,
            isOnline = true,
            isChatOpen = isOpen,
            onlineGraceSeconds = PresenceOnlineSeconds,
            lastSeenAt = now.ToString("yyyy-MM-ddTHH:mm:ss"),
        };
        if (broadcast)
            await _hub.Clients.All.SendAsync("HelpCenterChatPresenceChanged", payload, ct);
        return Ok(new { ok = true, payload });
    }

    [HttpPost("ActivityStatus")]
    public async Task<IActionResult> ActivityStatus([FromForm] string? activity, CancellationToken ct)
    {
        activity = activity?.Trim().ToLowerInvariant();
        if (activity is not ("typing" or "recording" or "idle")) activity = "idle";
        var userId = User.GetUserId()!;
        var now = IstanbulTimeHelper.Now;
        var presence = await _context.HelpCenterChatUserPresence.FirstOrDefaultAsync(item => item.UserId == userId, ct);
        if (presence is null)
        {
            presence = new HelpCenterChatUserPresence
            {
                UserId = userId,
                UserName = CurrentUserName(),
            };
            _context.HelpCenterChatUserPresence.Add(presence);
        }
        presence.LastSeenAt = now;
        presence.IsChatOpen = true;
        await _context.SaveChangesAsync(ct);
        var payload = new
        {
            userId,
            name = presence.UserName,
            imageUrl = presence.UserImageUrl ?? string.Empty,
            activity,
            expiresAt = now.AddSeconds(activity == "idle" ? 1 : 8).ToString("yyyy-MM-ddTHH:mm:ss"),
        };
        await _hub.Clients.All.SendAsync("HelpCenterChatActivityChanged", payload, ct);
        return Ok(new { ok = true, payload });
    }

    [HttpGet("ResolveReference")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> ResolveReference([FromQuery] string? value, CancellationToken ct)
    {
        var raw = CleanReference(value);
        if (string.IsNullOrWhiteSpace(raw))
            return NotFound(new { message = "لم يتم العثور على طلب مرتبط." });

        var digits = NormalizeDigits(raw);
        var looksLikeUrl = LooksLikeConversationUrl(raw);
        int? orderId = null;
        var referenceType = string.Empty;
        var referenceLabel = string.Empty;

        if (looksLikeUrl)
        {
            var normalizedUrl = NormalizeConversationUrl(raw);
            orderId = await _context.Orders.AsNoTracking()
                .Where(order => order.Chaturl != null &&
                    (order.Chaturl.Trim().Replace("https://", "").Replace("http://", "").Replace("www.", "") == normalizedUrl ||
                     order.Chaturl.Trim().Replace("https://", "").Replace("http://", "").Replace("www.", "") == normalizedUrl + "/"))
                .OrderByDescending(order => order.Id)
                .Select(order => (int?)order.Id)
                .FirstOrDefaultAsync(ct);
            if (orderId.HasValue)
            {
                referenceType = "ConversationUrl";
                referenceLabel = "رابط المحادثة";
            }
        }

        var compact = new string(raw.Where(character => !char.IsWhiteSpace(character)).ToArray());
        var hasStar = compact.Contains('*');
        var digitsAndStarsOnly = compact.Length > 0 && compact.All(character => char.IsDigit(character) || character == '*');
        var shipmentShape = !looksLikeUrl && digitsAndStarsOnly &&
            ((!hasStar && digits.Length is 5 or 6) || (hasStar && digits.Length is >= 1 and <= 12));

        if (!orderId.HasValue && shipmentShape && int.TryParse(digits, out var shipmentCode))
        {
            orderId = await _context.Orders.AsNoTracking()
                .Where(order => order.ExternalOrderId == shipmentCode || order.Id == shipmentCode)
                .OrderByDescending(order => order.ExternalOrderId == shipmentCode)
                .ThenByDescending(order => order.Id)
                .Select(order => (int?)order.Id)
                .FirstOrDefaultAsync(ct);
            if (orderId.HasValue)
            {
                referenceType = "ShipmentCode";
                referenceLabel = "كود الشحنة";
            }
        }

        if (!orderId.HasValue && !looksLikeUrl && !shipmentShape && digits.Length >= 7)
        {
            var compareDigits = digits.Length > 10 ? digits[^10..] : digits;
            orderId = await _context.Orders.AsNoTracking()
                .Where(order =>
                    order.TelephoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("/", "").EndsWith(compareDigits) ||
                    (order.SecondTelephoneNumber != null && order.SecondTelephoneNumber.Replace("+", "").Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Replace("/", "").EndsWith(compareDigits)))
                .OrderByDescending(order => order.Id)
                .Select(order => (int?)order.Id)
                .FirstOrDefaultAsync(ct);
            if (orderId.HasValue)
            {
                referenceType = "Phone";
                referenceLabel = "رقم الهاتف";
            }
        }

        if (!orderId.HasValue)
            return NotFound(new
            {
                message = looksLikeUrl
                    ? "لم يتم العثور على طلب مرتبط بهذا الرابط."
                    : "لم يتم العثور على طلب مرتبط بهذا الرقم.",
            });

        var order = await (
            from item in _context.Orders.AsNoTracking()
            join store in _context.ManufacturingCompanies.AsNoTracking()
                on item.ManufacturingCompanyId equals (int?)store.Id into stores
            from store in stores.DefaultIfEmpty()
            where item.Id == orderId.Value
            select new
            {
                item.Id,
                item.ExternalOrderId,
                item.Country,
                item.CustomerName,
                StoreName = store == null ? string.Empty : store.Name,
                StoreImageUrl = store == null ? string.Empty : store.ImageUrl,
                item.OrderStatus,
            })
            .FirstOrDefaultAsync(ct);
        if (order is null) return NotFound(new { message = "الطلب غير موجود." });

        var country = CountryCatalog.All.FirstOrDefault(item => item.Id == order.Country);
        return Ok(new
        {
            ok = true,
            orderId = order.Id,
            shipmentCode = order.ExternalOrderId ?? order.Id,
            customerName = order.CustomerName ?? string.Empty,
            countryName = country?.Name.Replace('_', ' ') ?? "دولة غير محددة",
            countryFlag = CountryFlag(order.Country),
            countryFlagUrl = country?.ImageUrl ?? string.Empty,
            storeName = order.StoreName ?? string.Empty,
            storeImageUrl = order.StoreImageUrl ?? string.Empty,
            orderStatus = OrderStatusCodes.GetDisplayName(order.OrderStatus),
            orderStatusValue = order.OrderStatus,
            referenceType,
            referenceLabel,
            referenceValue = raw,
            url = $"/Order/Details?id={order.Id}",
        });
    }

    [HttpPost("TriggerNegativeCommentsReminder")]
    [Authorize(Roles = "CallCenter")]
    [ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
    public async Task<IActionResult> TriggerNegativeCommentsReminder(CancellationToken ct)
    {
        if (CanManageAll())
            return Ok(new { ok = true, skipped = true, reason = "admin-account" });

        var currentUserId = User.GetUserId()!;
        var now = IstanbulTimeHelper.Now;
        var bucketStart = new DateTime(now.Year, now.Month, now.Day, now.Hour, (now.Minute / 5) * 5, 0, DateTimeKind.Unspecified);
        const string senderUserId = "system-negative-comments-reminder";
        var clientMessageId = $"cc-negative-5m-{bucketStart:yyyyMMddHHmm}";
        var existingId = await _context.HelpCenterChatMessages.AsNoTracking()
            .Where(item => item.SenderUserId == senderUserId && item.ClientMessageId == clientMessageId)
            .Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(ct);
        if (existingId.HasValue)
            return Ok(new
            {
                ok = true,
                inserted = false,
                skipped = true,
                messageId = existingId.Value,
                nextReminderAt = bucketStart.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss"),
            });

        var message = new HelpCenterChatMessage
        {
            SenderUserId = senderUserId,
            SenderName = "LUXIRAHOLDING",
            SenderImageUrl = "/images/luxira-holding-logo-email.png",
            MessageText = "@@الكل، يرجى مراجعة التعليقات السلبية الآن، والتأكد من تقييمها والتعامل معها واتخاذ الإجراء اللازم دون تأخير.",
            MessageKind = "Text",
            ClientMessageId = clientMessageId,
            CreatedAt = now,
        };
        _context.HelpCenterChatMessages.Add(message);
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            _context.Entry(message).State = EntityState.Detached;
            existingId = await _context.HelpCenterChatMessages.AsNoTracking()
                .Where(item => item.SenderUserId == senderUserId && item.ClientMessageId == clientMessageId)
                .Select(item => (long?)item.Id)
                .FirstOrDefaultAsync(ct);
            if (!existingId.HasValue) throw;
            return Ok(new
            {
                ok = true,
                inserted = false,
                skipped = true,
                messageId = existingId.Value,
                nextReminderAt = bucketStart.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss"),
            });
        }

        var mentionUserIds = await _context.Employees.AsNoTracking()
            .Where(employee => employee.ApplicationUserId != null && employee.ApplicationUserId != "" && employee.IsActive &&
                               _context.Users.Any(user => user.Id == employee.ApplicationUserId && user.EmailConfirmed))
            .Select(employee => employee.ApplicationUserId!)
            .Distinct()
            .ToListAsync(ct);
        if (mentionUserIds.Count == 0) mentionUserIds.Add(currentUserId);
        _context.HelpCenterChatMentions.AddRange(mentionUserIds.Select(userId => new HelpCenterChatMention
        {
            MessageId = message.Id,
            MentionedUserId = userId,
        }));
        await _context.SaveChangesAsync(ct);

        var payload = ToMessageDto(message);
        await _hub.Clients.All.SendAsync("HelpCenterChatMessageCreated", payload, ct);
        return Ok(new
        {
            ok = true,
            inserted = true,
            messageId = message.Id,
            message = payload,
            nextReminderAt = bucketStart.AddMinutes(5).ToString("yyyy-MM-ddTHH:mm:ss"),
        });
    }

    [HttpGet("GetKeywords")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetKeywords(
        [FromQuery] string? search,
        [FromQuery] string? type,
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        CancellationToken ct)
    {
        var query = _context.HelpCenterChatKeywords.AsNoTracking().AsQueryable();
        search = search?.Trim();
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(item => item.Phrase.Contains(search) || item.Category.Contains(search) ||
                (item.AutoReplyText != null && item.AutoReplyText.Contains(search)) ||
                (item.IncompleteAutoReplyText != null && item.IncompleteAutoReplyText.Contains(search)));
        if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(item => item.ActionType == type.Trim());
        if (!string.IsNullOrWhiteSpace(category) && !string.Equals(category, "All", StringComparison.OrdinalIgnoreCase))
            query = query.Where(item => item.Category == category.Trim());
        if (isActive.HasValue) query = query.Where(item => item.IsActive == isActive.Value);

        var keywords = await query.OrderByDescending(item => item.IsActive)
            .ThenByDescending(item => item.Id)
            .ToListAsync(ct);
        return Ok(new { ok = true, keywords });
    }

    [HttpPost("SaveKeyword")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> SaveKeyword([FromBody] HelpCenterChatKeywordRequest? request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Phrase))
            return Ok(new { ok = false, error = "يرجى كتابة نص الكلمة أو التعبير" });

        var now = IstanbulTimeHelper.Now;
        var userName = CurrentUserName();
        var actionType = string.IsNullOrWhiteSpace(request.ActionType) ? "AutoConversion" : request.ActionType.Trim();
        var category = string.IsNullOrWhiteSpace(request.Category) ? "عام" : request.Category.Trim();
        var autoReply = NullIfWhiteSpace(request.AutoReplyText);
        var incompleteReply = NullIfWhiteSpace(request.IncompleteAutoReplyText);

        if (request.Id > 0)
        {
            var keyword = await _context.HelpCenterChatKeywords.FirstOrDefaultAsync(item => item.Id == request.Id, ct);
            if (keyword is null) return Ok(new { ok = false, error = "الكلمة غير موجودة" });
            var phrase = request.Phrase.Trim();
            keyword.Phrase = phrase;
            keyword.NormalizedPhrase = NormalizeKeywordText(phrase);
            keyword.ActionType = actionType;
            keyword.Category = category;
            keyword.AutoReplyText = autoReply;
            keyword.IncompleteAutoReplyText = incompleteReply;
            keyword.IsActive = request.IsActive;
            keyword.UpdatedAt = now;
            keyword.UpdatedBy = userName;
            await _context.SaveChangesAsync(ct);
            return Ok(new { ok = true, message = "تم تعديل الكلمة / التعبير بنجاح", addedCount = 1, skippedCount = 0 });
        }

        var candidates = request.Phrase
            .Split(['\r', '\n', ',', '،', ';', '؛'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(phrase => (Phrase: phrase, Normalized: NormalizeKeywordText(phrase)))
            .Where(item => item.Normalized.Length > 0)
            .DistinctBy(item => item.Normalized, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (candidates.Count == 0)
            return Ok(new { ok = false, error = "يرجى كتابة كلمة أو جملة صالحة" });

        var normalizedPhrases = candidates.Select(item => item.Normalized).ToList();
        var existing = await _context.HelpCenterChatKeywords.AsNoTracking()
            .Where(item => normalizedPhrases.Contains(item.NormalizedPhrase))
            .Select(item => item.NormalizedPhrase)
            .ToHashSetAsync(StringComparer.OrdinalIgnoreCase, ct);
        var additions = candidates.Where(item => !existing.Contains(item.Normalized))
            .Select(item => new HelpCenterChatKeyword
            {
                Phrase = item.Phrase,
                NormalizedPhrase = item.Normalized,
                ActionType = actionType,
                Category = category,
                AutoReplyText = autoReply,
                IncompleteAutoReplyText = incompleteReply,
                IsActive = request.IsActive,
                CreatedAt = now,
                CreatedBy = userName,
            })
            .ToList();
        var skippedCount = candidates.Count - additions.Count;
        if (additions.Count == 0)
            return Ok(new
            {
                ok = false,
                error = skippedCount == 1
                    ? "هذه الكلمة أو الجملة موجودة بالفعل في النظام"
                    : "جميع الكلمات أو الجمل المدخلة موجودة بالفعل في النظام",
                addedCount = 0,
                skippedCount,
            });

        _context.HelpCenterChatKeywords.AddRange(additions);
        await _context.SaveChangesAsync(ct);
        var message = additions.Count == 1 && skippedCount == 0
            ? "تمت إضافة الكلمة / الجملة بنجاح"
            : skippedCount == 0
                ? $"تمت إضافة {additions.Count} كلمات/جمل بنجاح"
                : $"تمت إضافة {additions.Count} كلمات/جمل بنجاح (تم تخطي {skippedCount} موجودة مسبقاً)";
        return Ok(new { ok = true, message, addedCount = additions.Count, skippedCount });
    }

    [HttpPost("ToggleKeywordActive")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> ToggleKeywordActive([FromBody] ToggleKeywordActiveRequest? request, CancellationToken ct)
    {
        if (request is null || request.Id <= 0)
            return Ok(new { ok = false, error = "معرف الكلمة غير صحيح" });
        var affected = await _context.HelpCenterChatKeywords
            .Where(item => item.Id == request.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(item => item.IsActive, request.IsActive)
                .SetProperty(item => item.UpdatedAt, IstanbulTimeHelper.Now)
                .SetProperty(item => item.UpdatedBy, CurrentUserName()), ct);
        return Ok(new { ok = affected > 0, message = request.IsActive ? "تم تفعيل الكلمة" : "تم تعطيل الكلمة" });
    }

    [HttpPost("DeleteKeyword")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteKeyword([FromQuery] int id, CancellationToken ct)
    {
        if (id <= 0) return Ok(new { ok = false, error = "معرف الكلمة غير صحيح" });
        var affected = await _context.HelpCenterChatKeywords.Where(item => item.Id == id).ExecuteDeleteAsync(ct);
        return Ok(new { ok = affected > 0, message = "تم حذف الكلمة بنجاح" });
    }

    [HttpPost("DeleteKeywords")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> DeleteKeywords([FromBody] List<int>? ids, CancellationToken ct)
    {
        var validIds = ids?.Where(id => id > 0).Distinct().Take(1000).ToList() ?? [];
        if (validIds.Count == 0)
            return Ok(new { ok = false, error = ids is null or { Count: 0 } ? "يرجى تحديد الكلمات المراد حذفها" : "معرفات الكلمات غير صحيحة" });
        var affected = await _context.HelpCenterChatKeywords.Where(item => validIds.Contains(item.Id)).ExecuteDeleteAsync(ct);
        return Ok(new { ok = true, deletedCount = affected, message = $"تم حذف {affected} كلمة بنجاح" });
    }

    [HttpGet("GetKeywordCategories")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetKeywordCategories(CancellationToken ct)
    {
        var categories = await _context.HelpCenterChatKeywords.AsNoTracking()
            .Where(item => item.Category != "")
            .Select(item => item.Category)
            .Distinct()
            .OrderBy(item => item)
            .ToListAsync(ct);
        IReadOnlyCollection<string> result = categories.Count > 0 ? categories : DefaultKeywordCategories;
        return Ok(new { ok = true, categories = result });
    }

    [HttpGet("Media")]
    public async Task<IActionResult> Media([FromQuery] int take = 120, CancellationToken ct = default)
    {
        take = Math.Clamp(take, 1, 200);
        var items = await VisibleMessages()
            .Where(message => message.AttachmentStoragePath != null && message.AttachmentStoragePath != "")
            .OrderByDescending(message => message.Id)
            .Take(take)
            .ToListAsync(ct);
        return Ok(new
        {
            ok = true,
            items = items.Select(message => new
            {
                message.Id,
                message.SenderName,
                message.MessageKind,
                attachmentUrl = Url.Action(nameof(Attachment), new { id = message.Id }),
                message.AttachmentOriginalName,
                createdAt = message.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
            }),
        });
    }

    private static List<long> ParseMessageIds(string? ids) =>
        (ids ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => long.TryParse(value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .Take(500)
            .ToList();

    private IQueryable<HelpCenterChatMessage> VisibleMessages()
    {
        var userId = User.GetUserId()!;
        var query = _context.HelpCenterChatMessages.AsNoTracking();
        if (!CanManageAll()) query = query.Where(message => !message.IsDeleted);
        return query.Where(message => !_context.HelpCenterChatMessageHiddenForUsers
            .Any(hidden => hidden.MessageId == message.Id && hidden.UserId == userId));
    }

    private bool CanManageAll() =>
        User.IsInRole("Admin") || User.IsInRole("Administrator") || User.IsInRole("ExecutiveDirector");

    private string CurrentUserName() =>
        User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "موظف";

    private async Task<HelpCenterChatSetting> ReadSettingsAsync(CancellationToken ct) =>
        await _context.HelpCenterChatSettings.AsNoTracking().FirstOrDefaultAsync(item => item.Id == 1, ct) ??
        new HelpCenterChatSetting { Id = 1 };

    private object ToMessageDto(HelpCenterChatMessage message) => new
    {
        message.Id,
        message.SenderUserId,
        message.SenderName,
        senderImageUrl = message.SenderImageUrl ?? string.Empty,
        text = message.MessageText ?? string.Empty,
        message.MessageKind,
        attachmentUrl = string.IsNullOrWhiteSpace(message.AttachmentStoragePath)
            ? null
            : Url.Action(nameof(Attachment), new { id = message.Id }),
        message.AttachmentOriginalName,
        message.AttachmentMimeType,
        createdAt = message.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ss"),
        message.IsDeleted,
        isEdited = message.EditedAt.HasValue,
        editedAt = message.EditedAt?.ToString("yyyy-MM-ddTHH:mm:ss"),
        message.ReplyToMessageId,
    };

    [HttpGet("Attachment")]
    public async Task<IActionResult> Attachment([FromQuery] long id, CancellationToken ct)
    {
        var attachment = await VisibleMessages()
            .Where(message => message.Id == id)
            .Select(message => new { message.AttachmentStoragePath, message.AttachmentOriginalName })
            .FirstOrDefaultAsync(ct);
        if (attachment is null || string.IsNullOrWhiteSpace(attachment.AttachmentStoragePath)) return NotFound();
        return Redirect(_storage.GetPresignedUrl(attachment.AttachmentStoragePath));
    }

    private static string CleanReference(string? value) =>
        WebUtility.HtmlDecode(value ?? string.Empty)
            .Replace("\u200e", string.Empty)
            .Replace("\u200f", string.Empty)
            .Replace("\u202a", string.Empty)
            .Replace("\u202b", string.Empty)
            .Replace("\u202c", string.Empty)
            .Replace("\u202d", string.Empty)
            .Replace("\u202e", string.Empty)
            .Replace("\u2066", string.Empty)
            .Replace("\u2067", string.Empty)
            .Replace("\u2068", string.Empty)
            .Replace("\u2069", string.Empty)
            .Replace('\u00a0', ' ')
            .Trim();

    private static string NormalizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var buffer = new char[value.Length];
        var length = 0;
        foreach (var character in value)
        {
            if (character is >= '0' and <= '9') buffer[length++] = character;
            else if (character is >= '٠' and <= '٩') buffer[length++] = (char)('0' + character - '٠');
            else if (character is >= '۰' and <= '۹') buffer[length++] = (char)('0' + character - '۰');
        }
        return new string(buffer, 0, length);
    }

    private static bool LooksLikeConversationUrl(string value) =>
        value.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
        value.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
        (value.Contains('.') && !value.Contains(' '));

    private static string NormalizeConversationUrl(string value)
    {
        var normalized = WebUtility.HtmlDecode(value).Trim().ToLowerInvariant();
        if (normalized.StartsWith("https://", StringComparison.Ordinal)) normalized = normalized[8..];
        else if (normalized.StartsWith("http://", StringComparison.Ordinal)) normalized = normalized[7..];
        if (normalized.StartsWith("www.", StringComparison.Ordinal)) normalized = normalized[4..];
        return normalized.TrimEnd('/');
    }

    private static string CountryFlag(int country) => country switch
    {
        1 => "🇮🇶", 2 => "🇦🇪", 3 => "🇶🇦", 4 => "🇱🇾",
        5 => "🇴🇲", 6 => "🇵🇸", 7 => "🇹🇷", 8 => "🇯🇴",
        9 => "🇰🇼", 10 => "🇧🇭", 11 => "🇸🇦", 12 => "🇹🇳",
        13 => "🇲🇦", 14 => "🇩🇿", 15 => "🇱🇧", 16 => "🇪🇬",
        _ => "🌍",
    };

    private static string NormalizeKeywordText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var normalized = text
            .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ء', 'ا')
            .Replace('ة', 'ه').Replace('ى', 'ي').Replace('ئ', 'ي').Replace('ؤ', 'و')
            .Replace('؟', ' ').Replace('?', ' ').Replace('!', ' ').Replace('.', ' ')
            .Replace(',', ' ').Replace('،', ' ').Replace(':', ' ').Replace('-', ' ')
            .Replace('_', ' ').Replace('~', ' ').Replace('"', ' ').Replace('\'', ' ')
            .Replace('(', ' ').Replace(')', ' ').Replace('[', ' ').Replace(']', ' ')
            .Replace('{', ' ').Replace('}', ' ').Trim();
        while (normalized.Contains("  ", StringComparison.Ordinal))
            normalized = normalized.Replace("  ", " ", StringComparison.Ordinal);
        return normalized;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task ApplyQuestionOrderStatusRuleAsync(long messageId, int orderId, string recipientUserId, CancellationToken ct)
    {
        var messageText = await _context.HelpCenterChatMessages.AsNoTracking()
            .Where(item => item.Id == messageId && !item.IsDeleted)
            .Select(item => item.MessageText)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        var normalizedText = NormalizeKeywordText(messageText);
        var activeKeywords = await _context.HelpCenterChatKeywords.AsNoTracking()
            .Where(item => item.IsActive)
            .ToListAsync(ct);
        var matchedKeyword = activeKeywords
            .Where(item => normalizedText.Contains(
                string.IsNullOrWhiteSpace(item.NormalizedPhrase) ? NormalizeKeywordText(item.Phrase) : item.NormalizedPhrase,
                StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => !string.IsNullOrWhiteSpace(item.AutoReplyText))
            .ThenByDescending(item => item.NormalizedPhrase.Length)
            .FirstOrDefault();
        if (matchedKeyword is null && !IsTrackedOrderQuestion(normalizedText)) return;

        var order = await _context.Orders.FirstOrDefaultAsync(item => item.Id == orderId, ct);
        if (order is null) return;
        var now = IstanbulTimeHelper.Now;
        string? replyText = matchedKeyword?.AutoReplyText;

        if (IsIncompleteStatus(order.OrderStatus))
        {
            replyText = NullIfWhiteSpace(matchedKeyword?.IncompleteAutoReplyText) ??
                        NullIfWhiteSpace(matchedKeyword?.AutoReplyText) ?? "ابعت عنوان كامل";
            await SendAutomaticReplyAsync(messageId, replyText, recipientUserId, now, ct);
            return;
        }

        if (order.OrderStatus is OrderStatusCodes.New or OrderStatusCodes.Prepared or OrderStatusCodes.Processed)
        {
            await SendAutomaticReplyAsync(messageId, replyText, recipientUserId, now, ct);
            return;
        }

        var shouldMoveToProcessed = IsDirectToProcessedStatus(order.OrderStatus);
        if (order.OrderStatus is OrderStatusCodes.InDelivery or OrderStatusCodes.TemporarilyDelivered)
        {
            var statusStartedAt = await _context.OrderStatusHistories.AsNoTracking()
                .Where(history => history.OrderId == order.Id && history.Status == order.OrderStatus)
                .OrderByDescending(history => history.Id)
                .Select(history => (DateTime?)history.CreatedAt)
                .FirstOrDefaultAsync(ct) ?? order.CreatedDate;
            shouldMoveToProcessed = statusStartedAt <= now.AddDays(-3);
        }

        if (!shouldMoveToProcessed)
        {
            await SendAutomaticReplyAsync(messageId, replyText, recipientUserId, now, ct);
            return;
        }

        var systemUserId = await _context.Users.AsNoTracking()
            .Where(user => (user.Name != null && user.Name.Contains("لوكسيرا")) ||
                           (user.UserName != null && (user.UserName.Contains("لوكسيرا") || user.UserName == "system")) ||
                           (user.Email != null && user.Email.Contains("luxira")))
            .Select(user => user.Id)
            .FirstOrDefaultAsync(ct);
        var previousStatus = order.OrderStatus;
        order.OrderStatus = OrderStatusCodes.Processed;
        order.LastEditedDate = now;
        var history = new OrderStatusHistory
        {
            OrderId = order.Id,
            Status = OrderStatusCodes.Processed,
            CreatedAt = now,
            ApplicationUserId = systemUserId,
            Name = "شركة LUXIRA HOLDING التركية",
            Reason = "HelpCenterChatAutoConversion",
        };
        _context.OrderStatusHistories.Add(history);
        await _context.SaveChangesAsync(ct);

        var statusPayload = new
        {
            OrderId = order.Id,
            history.Id,
            history.Status,
            history.CreatedAt,
            history.ApplicationUserId,
            history.Reason,
            UserName = "شركة LUXIRA HOLDING التركية",
            UserImageUrl = "/images/luxira-help-center-logo.png",
            StatusPhrase = OrderStatusCodes.GetDisplayName(OrderStatusCodes.Processed),
            ColorStyle = string.Empty,
            FailureReasonImageUrl = string.Empty,
            PreviousStatus = previousStatus,
            NewStatus = OrderStatusCodes.Processed,
        };
        await Task.WhenAll(
            _hub.Clients.Group("UsersExpectDelivery").SendAsync("OrderStatusUpdated", statusPayload, ct),
            _hub.Clients.Group($"deliveryCompany_{order.DeliveryCompanyId}").SendAsync("OrderStatusUpdated", statusPayload, ct),
            _hub.Clients.Group($"manufacturingCompany_{order.ManufacturingCompanyId}").SendAsync("OrderStatusUpdated", statusPayload, ct));
        await _hub.Clients.All.SendAsync("OrderStatusUpdateFinalized", new
        {
            orderIds = new[] { order.Id },
            orders = new[]
            {
                new
                {
                    orderId = order.Id,
                    status = OrderStatusCodes.GetDisplayName(OrderStatusCodes.Processed),
                    statusValue = OrderStatusCodes.Processed,
                    statusPhrase = OrderStatusCodes.GetDisplayName(OrderStatusCodes.Processed),
                    colorStyle = string.Empty,
                },
            },
            updatedAt = now,
        }, ct);
        await SendAutomaticReplyAsync(messageId, replyText, recipientUserId, now.AddMilliseconds(50), ct);
    }

    private async Task SendAutomaticReplyAsync(
        long replyToMessageId,
        string? replyText,
        string recipientUserId,
        DateTime createdAt,
        CancellationToken ct)
    {
        var lines = (replyText ?? string.Empty)
            .Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (lines.Length == 0) return;
        var messages = lines.Select((line, index) => new HelpCenterChatMessage
        {
            SenderUserId = "00000000-0000-0000-0000-000000000000",
            SenderName = "شركة LUXIRA HOLDING التركية",
            SenderImageUrl = "/images/luxira-help-center-logo.png",
            MessageText = line,
            MessageKind = "Text",
            CreatedAt = createdAt.AddMilliseconds(index * 50),
            ReplyToMessageId = replyToMessageId,
            ClientMessageId = Guid.NewGuid().ToString("N"),
        }).ToList();
        _context.HelpCenterChatMessages.AddRange(messages);
        await _context.SaveChangesAsync(ct);
        foreach (var message in messages)
            await _hub.Clients.All.SendAsync("HelpCenterChatMessageCreated", ToMessageDto(message), ct);
    }

    private static bool IsTrackedOrderQuestion(string normalizedText)
    {
        string[] phrases =
        [
            "متى بيوصل", "امتى بيوصل", "بيوصل الطلب", "متى يوصل", "امتى يوصل",
            "الزبونه بدها", "الزبون بده", "بدها الطلب", "بده الطلب", "العميل الطلب ما وصله",
            "الطلب ما وصله", "ما وصله الطلب", "العميل ما وصله", "ما وصل الطلب",
            "الزبونه ما وصلها", "ما وصلها الطلب", "وين الطلب", "وين وصل", "فين الطلب",
            "فين وصل", "ما وصل", "ماوصل", "لم يصل", "ليش ما", "ليه ما", "تاخر",
            "شو صار", "ايش صار", "ايه صار", "استفسار", "تتبع", "حاله الطلب",
        ];
        return phrases.Any(phrase => normalizedText.Contains(phrase, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsIncompleteStatus(int status) => status is
        OrderStatusCodes.Incomplete or OrderStatusCodes.IncompleteStage1 or OrderStatusCodes.IncompleteStage2 or
        OrderStatusCodes.IncompleteStage3 or OrderStatusCodes.IncompleteStage4 or OrderStatusCodes.IncompleteStage5 or
        OrderStatusCodes.IncompleteStage6;

    private static bool IsDirectToProcessedStatus(int status) =>
        OrderStatusCodes.FailureStatuses.Contains(status) || status is OrderStatusCodes.WaitingForProcessing or
            OrderStatusCodes.Returned or OrderStatusCodes.ReferenceArchive or OrderStatusCodes.Postponed;
}

public sealed record SendChatMessageRequest(
    string MessageText,
    string? AttachmentStoragePath,
    string? AttachmentOriginalName,
    string? AttachmentMimeType,
    string? ClientMessageId,
    long? ReplyToMessageId);

public sealed record HelpCenterChatKeywordRequest(
    int Id,
    string Phrase,
    string? ActionType,
    string? Category,
    string? AutoReplyText,
    string? IncompleteAutoReplyText,
    bool IsActive = true);

public sealed record ToggleKeywordActiveRequest(int Id, bool IsActive);
