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
