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
    }
}
