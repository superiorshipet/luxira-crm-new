using System.ComponentModel.DataAnnotations.Schema;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.DeliveryCompanies.Models;

namespace Luxira.Api.Features.Orders.Models;

public class Order
{
    public int Id { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedAt { get; set; }
    public string? PinnedByUserId { get; set; }

    public int Country { get; set; }
    public string? State { get; set; }
    public int OrderSource { get; set; }
    public string? SourceName { get; set; }
    public int? ManufacturingCompanyId { get; set; }
    public int DeliveryCompanyId { get; set; }
    public DeliveryCompany? DeliveryCompany { get; set; }

    public string TelephoneNumber { get; set; } = string.Empty;
    public string? SecondTelephoneNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Address { get; set; } = string.Empty;

    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastEditedDate { get; set; }
    public DateTime? FixedOrderDate { get; set; }
    public DateTime? InstantAddedDate { get; set; }

    public int OrderStatus { get; set; } = OrderStatusCodes.New;
    public decimal TotalPrice { get; set; }
    public decimal DeliveryPrice { get; set; }
    public decimal CustomerDeliveryPrice { get; set; }
    public int? ExternalOrderId { get; set; }

    public string? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }

    public bool FromComments { get; set; }
    public bool Gender { get; set; }
    public bool IsHidden { get; set; }
    public bool IsDelayed { get; set; }
    public string? Fixedby { get; set; }
    public string? DelegateEmployeeId { get; set; }
    public bool IsPaid { get; set; }
    public string? Editedby { get; set; }
    public bool IsDiscount { get; set; }
    public bool IsClientSpecial { get; set; }
    public bool IsBonusPaidForEmployee { get; set; }
    public bool IsComplaints { get; set; }
    public bool IsBonus { get; set; }
    public int? BonusPaymentId { get; set; }
    public string? Chaturl { get; set; }
    public int? CampaignId { get; set; }
    public int? CreationDurationSeconds { get; set; }
    public string? PhotoUrl { get; set; }
    public string? PhotoS3Key { get; set; }
    public string? PaymentReceiptUrl { get; set; }
    public string? PaymentReceiptS3Key { get; set; }

    [NotMapped]
    public string? PostTrackNumber { get; set; }
    [NotMapped]
    public string? CamexShipmentId { get; set; }
    [NotMapped]
    public string? SandoogShipmentId { get; set; }

    public List<OrderWarehouse> OrderWarehouses { get; set; } = new();
    public List<OrderStatusHistory> StatusHistories { get; set; } = new();
    public List<OrderEditHistory> EditHistories { get; set; } = new();
}

public class OrderWarehouse
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int WarehouseId { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public decimal? Cost { get; set; }
}

public class OrderStatusHistory
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int? Status { get; set; }
    public string? Reason { get; set; }
    public string? ApplicationUserId { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public string? FailureReasonImageUrl { get; set; }
    public string? FailureReasonImageS3Key { get; set; }
    public string? Name { get; set; }
    public bool IsHidden { get; set; }
}

public class OrderEditHistory
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int EditNumber { get; set; }
    public int Country { get; set; }
    public string State { get; set; } = string.Empty;
    public int OrderSource { get; set; }
    public string? SourceName { get; set; }
    public int? ManufacturingCompanyId { get; set; }
    public int DeliveryCompanyId { get; set; }
    public string TelephoneNumber { get; set; } = string.Empty;
    public string? SecondTelephoneNumber { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public string Address { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public DateTime? LastEditedDate { get; set; }
    public DateTime? FixedOrderDate { get; set; }
    public DateTime? InstantAddedDate { get; set; }
    public int OrderStatus { get; set; }
    public decimal TotalPrice { get; set; }
    public int? ExternalOrderId { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string? ExternalShipmentCode { get; set; }
    public bool FromComments { get; set; }
    public bool Gender { get; set; }
    public bool IsPaid { get; set; }
    public string? Editedby { get; set; }
    public bool FromOffers { get; set; }
    public int? CampaignId { get; set; }
    public decimal DeliveryPrice { get; set; }
    public string? Chaturl { get; set; }
}

public class OrderReport
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public List<OrderReportOrder> ReportOrders { get; set; } = new();
}

public class OrderReportOrder
{
    public int Id { get; set; }
    public int OrderReportId { get; set; }
    public OrderReport? OrderReport { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
}

public class OrderBonusConfiguration
{
    public int Id { get; set; }
    public int Country { get; set; }
    public decimal BonusAmount { get; set; }
    public bool IsActive { get; set; } = true;
}

public class OrderPost
{
    public int Id { get; set; }
    public int Country { get; set; }
    public string PostId { get; set; } = string.Empty;
    public string? PostUrl { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? StoreName { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class OrderFollowUpRequest
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
    public bool IsResolved { get; set; }
}
