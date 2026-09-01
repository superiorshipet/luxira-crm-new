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
    public int Id { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Direction { get; set; } = "Outbound"; // Inbound, Outbound
    public string Status { get; set; } = "Sent"; // Sent, Delivered, Read, Failed
    public int? OrderId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class AdminNotification
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Type { get; set; } = "Info"; // Info, Warning, Alert, Order
    public string? TargetUserId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
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
