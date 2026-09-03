using Luxira.Api.Core;
using Luxira.Api.Features.Orders.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Luxira.Api.Features.Orders;

public class OrderDbConfig : IDbConfig<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.CustomerName).HasMaxLength(255).IsRequired();
        builder.Property(o => o.TelephoneNumber).HasMaxLength(255).IsRequired();
        builder.Property(o => o.SecondTelephoneNumber).HasMaxLength(255);
        builder.Property(o => o.Address).HasMaxLength(255).IsRequired();
        builder.Property(o => o.State).HasMaxLength(255);
        builder.Property(o => o.SourceName).HasMaxLength(255);
        builder.Property(o => o.Notes).HasMaxLength(255);
        builder.Property(o => o.TotalPrice).HasPrecision(18, 2);
        builder.Property(o => o.DeliveryPrice).HasPrecision(18, 2);
        builder.Property(o => o.CustomerDeliveryPrice).HasPrecision(18, 2);
        builder.Property(o => o.PhotoS3Key).HasMaxLength(450);
        builder.Property(o => o.PaymentReceiptS3Key).HasMaxLength(450);
        builder.Property(o => o.SandoogReasonCode).HasMaxLength(64);

        builder.HasOne(o => o.DeliveryCompany)
            .WithMany()
            .HasForeignKey(o => o.DeliveryCompanyId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(o => o.ApplicationUser)
            .WithMany()
            .HasForeignKey(o => o.ApplicationUserId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(o => o.OrderWarehouses)
            .WithOne(w => w.Order)
            .HasForeignKey(w => w.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.StatusHistories)
            .WithOne(h => h.Order)
            .HasForeignKey(h => h.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(o => o.EditHistories)
            .WithOne(e => e.Order)
            .HasForeignKey(e => e.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class OrderWarehouseDbConfig : IDbConfig<OrderWarehouse>
{
    public void Configure(EntityTypeBuilder<OrderWarehouse> builder)
    {
        builder.ToTable("OrderWarehouses");
        builder.HasKey(w => new { w.OrderId, w.WarehouseId });
        builder.Property(w => w.UnitPrice).HasPrecision(18, 2);
    }
}

public class OrderStatusHistoryDbConfig : IDbConfig<OrderStatusHistory>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistory> builder)
    {
        builder.ToTable("OrderStatusHistories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Reason).HasMaxLength(255);
        builder.Property(h => h.FailureReasonImageS3Key).HasMaxLength(450);
    }
}

public class OrderStatusHistoryDeliveryCompanySnapshotDbConfig : IDbConfig<OrderStatusHistoryDeliveryCompanySnapshot>
{
    public void Configure(EntityTypeBuilder<OrderStatusHistoryDeliveryCompanySnapshot> builder)
    {
        builder.ToTable("OrderStatusHistoryDeliveryCompanySnapshots");
        builder.HasKey(snapshot => snapshot.OrderStatusHistoryId);
        builder.Property(snapshot => snapshot.DeliveryCompanyName).HasMaxLength(300);
        builder.HasIndex(snapshot => new { snapshot.OrderId, snapshot.OrderStatusHistoryId });
    }
}

public class StatusUpdateBatchLogDbConfig : IDbConfig<StatusUpdateBatchLog>
{
    public void Configure(EntityTypeBuilder<StatusUpdateBatchLog> builder)
    {
        builder.ToTable("StatusUpdateBatchLogs");
        builder.HasKey(log => log.Id);
        builder.Property(log => log.EmployeeUserId).HasMaxLength(450);
        builder.Property(log => log.EmployeeName).HasMaxLength(250);
        builder.Property(log => log.EmployeeImageUrl).HasMaxLength(1000);
        builder.Property(log => log.CountryName).HasMaxLength(120);
        builder.Property(log => log.StoreName).HasMaxLength(250);
        builder.Property(log => log.FinalStatusName).HasMaxLength(120).IsRequired();
        builder.HasIndex(log => log.UpdatedAt);
        builder.HasIndex(log => log.BatchKey);
        builder.HasIndex(log => new { log.EmployeeUserId, log.UpdatedAt });
        builder.HasMany(log => log.Items).WithOne(item => item.BatchLog)
            .HasForeignKey(item => item.BatchLogId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class StatusUpdateBatchLogItemDbConfig : IDbConfig<StatusUpdateBatchLogItem>
{
    public void Configure(EntityTypeBuilder<StatusUpdateBatchLogItem> builder)
    {
        builder.ToTable("StatusUpdateBatchLogItems");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.OrderCode).HasMaxLength(80).IsRequired();
        builder.Property(item => item.FinalStatusName).HasMaxLength(120).IsRequired();
        builder.Property(item => item.FailureReason).HasMaxLength(500);
        builder.Property(item => item.DeliveryCompanyName).HasMaxLength(250);
        builder.Property(item => item.CountryName).HasMaxLength(120);
        builder.Property(item => item.StoreName).HasMaxLength(250);
        builder.HasIndex(item => item.BatchLogId);
        builder.HasIndex(item => item.OrderId);
        builder.HasIndex(item => item.UpdatedAt);
    }
}

public class OrderEditHistoryDbConfig : IDbConfig<OrderEditHistory>
{
    public void Configure(EntityTypeBuilder<OrderEditHistory> builder)
    {
        builder.ToTable("OrderEditHistories");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.TotalPrice).HasPrecision(18, 2);
        builder.Property(e => e.DeliveryPrice).HasPrecision(18, 2);
    }
}

public class OrderReportDbConfig : IDbConfig<OrderReport>
{
    public void Configure(EntityTypeBuilder<OrderReport> builder)
    {
        builder.ToTable("OrderReports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.TotalAmount).HasPrecision(18, 2);
    }
}

public class OrderReportOrderDbConfig : IDbConfig<OrderReportOrder>
{
    public void Configure(EntityTypeBuilder<OrderReportOrder> builder)
    {
        builder.ToTable("OrderReportOrders");
        builder.HasKey(ro => new { ro.OrderReportId, ro.OrderId });
        builder.HasOne(ro => ro.OrderReport)
            .WithMany(r => r.ReportOrders)
            .HasForeignKey(ro => ro.OrderReportId);
        builder.HasOne(ro => ro.Order)
            .WithMany()
            .HasForeignKey(ro => ro.OrderId);
    }
}

public class OrderBonusConfigurationDbConfig : IDbConfig<OrderBonusConfiguration>
{
    public void Configure(EntityTypeBuilder<OrderBonusConfiguration> builder)
    {
        builder.ToTable("OrderBonusConfigurations");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.OrderThreshold).HasPrecision(18, 2);
        builder.Property(b => b.FlatBonusAmount).HasPrecision(18, 2);
        builder.Property(b => b.PercentageBonus).HasPrecision(18, 2);
    }
}

public class OrderPostDbConfig : IDbConfig<OrderPost>
{
    public void Configure(EntityTypeBuilder<OrderPost> builder)
    {
        builder.ToTable("OrderPosts");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.AuthorUserId).HasMaxLength(450).IsRequired();
        builder.HasOne(p => p.Order)
            .WithMany()
            .HasForeignKey(p => p.OrderId);
    }
}

public class OrderPostImageDbConfig : IDbConfig<OrderPostImage>
{
    public void Configure(EntityTypeBuilder<OrderPostImage> builder)
    {
        builder.ToTable("OrderPostImages");
        builder.HasKey(image => image.Id);
        builder.Property(image => image.Url).HasMaxLength(512).IsRequired();
        builder.Property(image => image.S3Key).HasMaxLength(450);
        builder.HasIndex(image => image.S3Key);
        builder.HasOne(image => image.OrderPost)
            .WithMany(post => post.Images)
            .HasForeignKey(image => image.OrderPostId);
    }
}

public class OrderPostDeletedHistoryDbConfig : IDbConfig<OrderPostDeletedHistory>
{
    public void Configure(EntityTypeBuilder<OrderPostDeletedHistory> builder)
    {
        builder.ToTable("OrderPostDeletedHistories");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.AuthorUserId).HasMaxLength(450);
        builder.Property(item => item.AuthorName).HasMaxLength(256);
        builder.Property(item => item.DeletedByUserId).HasMaxLength(450);
        builder.Property(item => item.DeletedByName).HasMaxLength(256);
        builder.HasIndex(item => item.OrderPostId).IsUnique();
        builder.HasIndex(item => new { item.OrderId, item.Type, item.DeletedAt });
    }
}

public class OrderPostEmployeeDeductionDbConfig : IDbConfig<OrderPostEmployeeDeduction>
{
    public void Configure(EntityTypeBuilder<OrderPostEmployeeDeduction> builder)
    {
        builder.ToTable("OrderPostEmployeeDeductions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.EmployeeName).HasMaxLength(256);
        builder.Property(item => item.Amount).HasPrecision(18, 2);
        builder.Property(item => item.OrderTotal).HasPrecision(18, 2);
        builder.Property(item => item.CreatedByUserId).HasMaxLength(450);
        builder.Property(item => item.CreatedByName).HasMaxLength(256);
        builder.HasIndex(item => new { item.OrderId, item.CreatedAt });
        builder.HasIndex(item => item.EmployeeTransactionId);
    }
}

public class OrderMetaActionClickDbConfig : IDbConfig<OrderMetaActionClick>
{
    public void Configure(EntityTypeBuilder<OrderMetaActionClick> builder)
    {
        builder.ToTable("OrderMetaActionClicks");
        builder.HasKey(click => click.Id);
        builder.Property(click => click.UserId).HasMaxLength(450);
        builder.Property(click => click.EmployeeName).HasMaxLength(300);
        builder.Property(click => click.Reason).HasMaxLength(100).IsRequired();
        builder.Property(click => click.OtherText).HasMaxLength(500);
        builder.Property(click => click.MetaUrl).HasMaxLength(1000);
        builder.Property(click => click.ContactType).HasMaxLength(40);
        builder.HasIndex(click => new { click.OrderId, click.ClickedAt });
    }
}

public class OrderFollowUpRequestDbConfig : IDbConfig<OrderFollowUpRequest>
{
    public void Configure(EntityTypeBuilder<OrderFollowUpRequest> builder)
    {
        builder.ToTable("OrderFollowUpRequests");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.RequestType).HasMaxLength(30).IsRequired();
        builder.Property(f => f.Note).HasMaxLength(2000);
        builder.Property(f => f.ImagePath).HasMaxLength(1000);
        builder.Property(f => f.ImageS3Key).HasMaxLength(450);
        builder.Property(f => f.CreatedByUserId).HasMaxLength(450);
        builder.Property(f => f.CreatedByName).HasMaxLength(250);
        builder.Property(f => f.ClosedByUserId).HasMaxLength(450);
        builder.Property(f => f.ClosedByName).HasMaxLength(250);
        builder.Property(f => f.ProcessingStartedByUserId).HasMaxLength(450);
        builder.Property(f => f.ProcessingStartedByName).HasMaxLength(250);
    }
}

public class PotentialOrderDbConfig : IDbConfig<PotentialOrder>
{
    public void Configure(EntityTypeBuilder<PotentialOrder> builder)
    {
        builder.ToTable("PotentialOrders");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.CustomerName).HasMaxLength(255);
        builder.Property(p => p.PhoneNumber).HasMaxLength(50);
        builder.Property(p => p.StoreName).HasMaxLength(255).IsRequired();
    }
}

public class UrgentReportDbConfig : IDbConfig<UrgentReport>
{
    public void Configure(EntityTypeBuilder<UrgentReport> builder)
    {
        builder.ToTable("UrgentReports");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.ReportType).HasMaxLength(100).IsRequired();
        builder.Property(r => r.Status).HasMaxLength(50);
        builder.Property(r => r.ScreenshotS3Key).HasMaxLength(450);
        builder.HasOne(r => r.Employee).WithMany().HasForeignKey(r => r.EmployeeId).OnDelete(DeleteBehavior.Restrict);
    }
}
