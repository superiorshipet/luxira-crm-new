namespace Luxira.Api.Features.Communication.Models;

public class HelpCenterChatMessage
{
    public long Id { get; set; }
    public string SenderUserId { get; set; } = string.Empty;
    public string SenderName { get; set; } = string.Empty;
    public string? SenderImageUrl { get; set; }
    public string? MessageText { get; set; }
    public string MessageKind { get; set; } = "Text";
    public string? AttachmentStoragePath { get; set; }
    public string? AttachmentOriginalName { get; set; }
    public string? AttachmentMimeType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByName { get; set; }
    public long? ReplyToMessageId { get; set; }
    public DateTime? EditedAt { get; set; }
    public string? ClientMessageId { get; set; }
}

public class HelpCenterChatReadState
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long LastReadMessageId { get; set; }
    public DateTime? LastReadAt { get; set; }
}

public class HelpCenterChatMessageEdit
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public string EditorUserId { get; set; } = string.Empty;
    public string EditorName { get; set; } = string.Empty;
    public string? OldMessageText { get; set; }
    public string? NewMessageText { get; set; }
    public DateTime EditedAt { get; set; }
}

public class HelpCenterChatReaction
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Emoji { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class HelpCenterChatPin
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public long MessageId { get; set; }
    public DateTime PinnedAt { get; set; }
}

public class HelpCenterChatMessageRead
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserImageUrl { get; set; }
    public DateTime ReadAt { get; set; }
}

public class HelpCenterChatMention
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public string MentionedUserId { get; set; } = string.Empty;
}

public class HelpCenterChatSetting
{
    public int Id { get; set; }
    public bool IsMuted { get; set; }
    public bool IsReadOnly { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
}

public class HelpCenterChatMessageOrderLink
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public int OrderId { get; set; }
    public string LinkedByUserId { get; set; } = string.Empty;
    public string LinkedByName { get; set; } = string.Empty;
    public DateTime LinkedAt { get; set; }
}

public class HelpCenterChatMessageHiddenForUser
{
    public long Id { get; set; }
    public long MessageId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime HiddenAt { get; set; }
}

public class HelpCenterChatUserPresence
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string? UserImageUrl { get; set; }
    public DateTime LastSeenAt { get; set; }
    public bool IsChatOpen { get; set; }
}

public class HelpCenterChatKeyword
{
    public int Id { get; set; }
    public string Phrase { get; set; } = string.Empty;
    public string NormalizedPhrase { get; set; } = string.Empty;
    public string ActionType { get; set; } = "AutoConversion";
    public string Category { get; set; } = "عام";
    public string? AutoReplyText { get; set; }
    public string? IncompleteAutoReplyText { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
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

public class AdminNotificationReplyState
{
    public int AdminNotificationId { get; set; }
    public bool RequiresReply { get; set; }
    public string? ReplyText { get; set; }
    public DateTimeOffset? RepliedAt { get; set; }
    public bool ReplySeenByAdmin { get; set; }
}

public class SystemEmailLog
{
    public const string DirectionOutgoing = "Outgoing";
    public const string DirectionIncoming = "Incoming";

    public long Id { get; set; }
    public string ToEmail { get; set; } = string.Empty;
    public string? FromEmail { get; set; }
    public string? RecipientName { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string EmailType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Direction { get; set; } = DirectionOutgoing;
    public string? MessageId { get; set; }
    public DateTime SentAt { get; set; }
    public string? ErrorMessage { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? RelatedEntityId { get; set; }
    public string? AttachmentName { get; set; }
    public string? BodyPreview { get; set; }
    public string? TriggeredByUserId { get; set; }
    public string? TriggeredByName { get; set; }
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
