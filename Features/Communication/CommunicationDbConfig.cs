using Luxira.Api.Core;
using Luxira.Api.Features.Communication.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Communication;

public class HelpCenterChatMessageDbConfig : IDbConfig<HelpCenterChatMessage>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatMessage> builder)
    {
        builder.ToTable("HelpCenterChatMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.SenderUserId).HasMaxLength(450).IsRequired();
        builder.Property(m => m.SenderName).HasMaxLength(250).IsRequired();
        builder.Property(m => m.SenderImageUrl).HasMaxLength(1000);
        builder.Property(m => m.MessageKind).HasMaxLength(20).IsRequired();
        builder.Property(m => m.AttachmentStoragePath).HasMaxLength(1000);
        builder.Property(m => m.AttachmentOriginalName).HasMaxLength(255);
        builder.Property(m => m.AttachmentMimeType).HasMaxLength(150);
        builder.HasIndex(m => new { m.IsDeleted, m.Id });
    }
}

public class HelpCenterChatReadStateDbConfig : IDbConfig<HelpCenterChatReadState>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatReadState> builder)
    {
        builder.ToTable("HelpCenterChatReadStates");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(item => item.UserId).IsUnique();
    }
}

public class HelpCenterChatMessageEditDbConfig : IDbConfig<HelpCenterChatMessageEdit>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatMessageEdit> builder)
    {
        builder.ToTable("HelpCenterChatMessageEdits");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EditorUserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.EditorName).HasMaxLength(250).IsRequired();
        builder.HasIndex(item => new { item.MessageId, item.Id });
    }
}

public class HelpCenterChatReactionDbConfig : IDbConfig<HelpCenterChatReaction>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatReaction> builder)
    {
        builder.ToTable("HelpCenterChatReactions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.UserName).HasMaxLength(250).IsRequired();
        builder.Property(item => item.Emoji).HasMaxLength(20).IsRequired();
        builder.HasIndex(item => new { item.MessageId, item.UserId, item.Emoji }).IsUnique();
    }
}

public class HelpCenterChatPinDbConfig : IDbConfig<HelpCenterChatPin>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatPin> builder)
    {
        builder.ToTable("HelpCenterChatPins");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(item => new { item.UserId, item.MessageId }).IsUnique();
    }
}

public class HelpCenterChatMessageReadDbConfig : IDbConfig<HelpCenterChatMessageRead>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatMessageRead> builder)
    {
        builder.ToTable("HelpCenterChatMessageReads");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.UserName).HasMaxLength(250).IsRequired();
        builder.Property(item => item.UserImageUrl).HasMaxLength(1000);
        builder.HasIndex(item => new { item.MessageId, item.UserId }).IsUnique();
    }
}

public class HelpCenterChatMentionDbConfig : IDbConfig<HelpCenterChatMention>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatMention> builder)
    {
        builder.ToTable("HelpCenterChatMentions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.MentionedUserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(item => new { item.MessageId, item.MentionedUserId }).IsUnique();
    }
}

public class HelpCenterChatSettingDbConfig : IDbConfig<HelpCenterChatSetting>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatSetting> builder)
    {
        builder.ToTable("HelpCenterChatSettings");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.UpdatedByUserId).HasMaxLength(450);
        builder.Property(item => item.UpdatedByName).HasMaxLength(250);
    }
}

public class HelpCenterChatMessageOrderLinkDbConfig : IDbConfig<HelpCenterChatMessageOrderLink>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatMessageOrderLink> builder)
    {
        builder.ToTable("HelpCenterChatMessageOrderLinks");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.LinkedByUserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.LinkedByName).HasMaxLength(250).IsRequired();
        builder.HasIndex(item => new { item.MessageId, item.OrderId }).IsUnique();
    }
}

public class HelpCenterChatMessageHiddenForUserDbConfig : IDbConfig<HelpCenterChatMessageHiddenForUser>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatMessageHiddenForUser> builder)
    {
        builder.ToTable("HelpCenterChatMessageHiddenForUsers");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.HasIndex(item => new { item.MessageId, item.UserId }).IsUnique();
    }
}

public class HelpCenterChatUserPresenceDbConfig : IDbConfig<HelpCenterChatUserPresence>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatUserPresence> builder)
    {
        builder.ToTable("HelpCenterChatUserPresence");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.UserId).HasMaxLength(450).IsRequired();
        builder.Property(item => item.UserName).HasMaxLength(250).IsRequired();
        builder.Property(item => item.UserImageUrl).HasMaxLength(1000);
        builder.HasIndex(item => item.UserId).IsUnique();
        builder.HasIndex(item => item.LastSeenAt);
    }
}

public class HelpCenterChatKeywordDbConfig : IDbConfig<HelpCenterChatKeyword>
{
    public void Configure(EntityTypeBuilder<HelpCenterChatKeyword> builder)
    {
        builder.ToTable("HelpCenterChatKeywords");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Phrase).HasMaxLength(250).IsRequired();
        builder.Property(item => item.NormalizedPhrase).HasMaxLength(250).IsRequired();
        builder.Property(item => item.ActionType).HasMaxLength(50).IsRequired();
        builder.Property(item => item.Category).HasMaxLength(100).IsRequired();
        builder.Property(item => item.AutoReplyText).HasColumnType("nvarchar(max)");
        builder.Property(item => item.IncompleteAutoReplyText).HasColumnType("nvarchar(max)");
        builder.Property(item => item.CreatedAt).HasColumnType("datetime");
        builder.Property(item => item.CreatedBy).HasMaxLength(128);
        builder.Property(item => item.UpdatedAt).HasColumnType("datetime");
        builder.Property(item => item.UpdatedBy).HasMaxLength(128);
        builder.HasIndex(item => new { item.IsActive, item.ActionType });
    }
}

public class WhatsAppMessageDbConfig : IDbConfig<WhatsAppMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppMessage> builder)
    {
        builder.ToTable("WhatsAppAutomationSendLogs");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.RecipientPhoneNumber).HasMaxLength(40).IsRequired();
        builder.Property(m => m.ProviderMessageId).HasMaxLength(80);
        builder.Property(m => m.ErrorMessage).HasMaxLength(1000);
    }
}

public class AdminNotificationDbConfig : IDbConfig<AdminNotification>
{
    public void Configure(EntityTypeBuilder<AdminNotification> builder)
    {
        builder.ToTable("AdminEmployeeNotifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.RecipientUserId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.RecipientName).HasMaxLength(200).IsRequired();
        builder.Property(n => n.CreatedByAdminUserId).HasMaxLength(450).IsRequired();
        builder.Property(n => n.CreatedByAdminName).HasMaxLength(200).IsRequired();
    }
}

public class ConferenceMeetingDbConfig : IDbConfig<ConferenceMeeting>
{
    public void Configure(EntityTypeBuilder<ConferenceMeeting> builder)
    {
        builder.ToTable("ConferenceMeetings");
        builder.HasKey(c => c.Id);
    }
}

public class PasswordEmailDbConfig : IDbConfig<PasswordEmail>
{
    public void Configure(EntityTypeBuilder<PasswordEmail> builder)
    {
        builder.ToTable("PasswordEmails");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Email).HasMaxLength(256).IsRequired();
        builder.Property(item => item.Password).HasMaxLength(500).IsRequired();
        builder.Property(item => item.PhoneNumber).HasMaxLength(80);
        builder.Property(item => item.PageStatusName).HasMaxLength(200);
        builder.HasOne(item => item.ManufacturingCompany)
            .WithMany()
            .HasForeignKey(item => item.ManufacturingCompanyId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(item => item.Histories)
            .WithOne(history => history.PasswordEmail)
            .HasForeignKey(history => history.PasswordEmailId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class PasswordEmailHistoryDbConfig : IDbConfig<PasswordEmailHistory>
{
    public void Configure(EntityTypeBuilder<PasswordEmailHistory> builder)
    {
        builder.ToTable("PasswordEmailHistories");
        builder.HasKey(history => history.Id);
        builder.Property(history => history.ActionType).HasMaxLength(30).IsRequired();
    }
}
