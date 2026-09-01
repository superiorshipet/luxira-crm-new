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
