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
        builder.ToTable("WhatsAppMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.PhoneNumber).HasMaxLength(50);
    }
}

public class AdminNotificationDbConfig : IDbConfig<AdminNotification>
{
    public void Configure(EntityTypeBuilder<AdminNotification> builder)
    {
        builder.ToTable("AdminNotifications");
        builder.HasKey(n => n.Id);
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
