namespace Luxira.Api.Features.Communication.Models;

public class HelpCenterChatMessage
{
    public int Id { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string? ReceiverUserId { get; set; }
    public string MessageText { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}

public class WhatsAppMessage
{
    public long Id { get; set; }
    public int? AccountId { get; set; }
    public int? TemplateId { get; set; }
    public int? OrderId { get; set; }
    public string RecipientPhoneNumber { get; set; } = string.Empty;
    public int EventType { get; set; }
    public int? OrderStatus { get; set; }
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AdminNotification
{
    public int Id { get; set; }
    public string RecipientUserId { get; set; } = string.Empty;
    public int? RecipientEmployeeId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string CreatedByAdminUserId { get; set; } = string.Empty;
    public string CreatedByAdminName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsRead { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public string IconUrl { get; set; } = string.Empty;
}

public class ConferenceMeeting
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public DateTime ScheduledStartTime { get; set; }
    public DateTime? ScheduledEndTime { get; set; }
    public string HostUserId { get; set; } = string.Empty;
}
