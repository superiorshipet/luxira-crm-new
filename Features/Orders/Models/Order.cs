using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Features.Warehouses.Models;

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

    public bool SandoogLegacyManual { get; set; }
    public DateTime? SandoogConfirmedAt { get; set; }
    public string? SandoogOrderId { get; set; }
    public string? SandoogFulfillmentId { get; set; }
    public string? SandoogDeliveryLabelUrl { get; set; }
    public string? SandoogReason { get; set; }
    public string? SandoogReasonCode { get; set; }
    public DateTime? SandoogLastAttemptAt { get; set; }
    public int SandoogSendAttempts { get; set; }
    public int SandoogSendGeneration { get; set; }

    public bool CamexLegacyManual { get; set; }
    public DateTime? CamexConfirmedAt { get; set; }
    public long? CamexTrackingNumber { get; set; }
    public int? CamexState { get; set; }
    public DateTime? CamexStateChangedAt { get; set; }
    public DateTime? CamexLastAttemptAt { get; set; }
    public int CamexSendAttempts { get; set; }
    public int CamexSendGeneration { get; set; }
    public string? CamexAdvisoryNote { get; set; }
    public DateTime? CamexAdvisoryAt { get; set; }
    public int? CamexAdvisoryState { get; set; }

    public List<OrderWarehouse> OrderWarehouses { get; set; } = new();
    public List<OrderStatusHistory> StatusHistories { get; set; } = new();
    public List<OrderEditHistory> EditHistories { get; set; } = new();
}

public class OrderWarehouse
{
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public int WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public int Amount { get; set; }
    public decimal? UnitPrice { get; set; }
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

public class OrderStatusHistoryDeliveryCompanySnapshot
{
    public int OrderStatusHistoryId { get; set; }
    public int OrderId { get; set; }
    public int? DeliveryCompanyId { get; set; }
    public string? DeliveryCompanyName { get; set; }
    public DateTime CapturedAt { get; set; }
}

public class StatusUpdateBatchLog
{
    public int Id { get; set; }
    public Guid BatchKey { get; set; }
    public string? EmployeeUserId { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeImageUrl { get; set; }
    public string? CountryName { get; set; }
    public int? StoreId { get; set; }
    public string? StoreName { get; set; }
    public int FinalStatusValue { get; set; }
    public string FinalStatusName { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<StatusUpdateBatchLogItem> Items { get; set; } = [];
}

public class StatusUpdateBatchLogItem
{
    public int Id { get; set; }
    public int BatchLogId { get; set; }
    public StatusUpdateBatchLog? BatchLog { get; set; }
    public int OrderId { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public int FinalStatusValue { get; set; }
    public string FinalStatusName { get; set; } = string.Empty;
    public string? FailureReason { get; set; }
    public string? DeliveryCompanyName { get; set; }
    public string? CountryName { get; set; }
    public string? StoreName { get; set; }
    public DateTime UpdatedAt { get; set; }
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
    public DateTime GeneratedTime { get; set; }
    public decimal TotalAmount { get; set; }
    public int? Country { get; set; }
    public int? DeliveryCompanyId { get; set; }
    public int OrderStatus { get; set; }
    public List<OrderReportOrder> ReportOrders { get; set; } = new();
}

public class OrderReportOrder
{
    public int OrderReportId { get; set; }
    public OrderReport? OrderReport { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
}

public class OrderBonusConfiguration
{
    public int Id { get; set; }
    public decimal OrderThreshold { get; set; }
    public decimal FlatBonusAmount { get; set; }
    public decimal? PercentageBonus { get; set; }
    public int Country { get; set; }
    public int? EmployeeId { get; set; }
}

public enum OrderPostType
{
    Problem = 0,
    EditNote = 1,
    OrderNote = 2
}

public class OrderPost
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public OrderPostType Type { get; set; }
    public string AuthorUserId { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<OrderPostImage> Images { get; set; } = new();
}

public class OrderPostImage
{
    public int Id { get; set; }
    public int OrderPostId { get; set; }
    public OrderPost? OrderPost { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? S3Key { get; set; }
    public DateTime? MigratedToS3At { get; set; }
    public int SortOrder { get; set; }
    public long? PHash { get; set; }
}

public class OrderPostDeletedHistory
{
    public int Id { get; set; }
    public int OrderPostId { get; set; }
    public int OrderId { get; set; }
    public int Type { get; set; }
    public string? Body { get; set; }
    public string? AuthorUserId { get; set; }
    public string? AuthorName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public DateTime DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByName { get; set; }
}

public class OrderPostEmployeeDeduction
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public int EmployeeId { get; set; }
    public string? EmployeeName { get; set; }
    public decimal Amount { get; set; }
    public decimal OrderTotal { get; set; }
    public string? Reason { get; set; }
    public string? ProblemText { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public int? EmployeeTransactionId { get; set; }
}

public class OrderMetaActionClick
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string? UserId { get; set; }
    public string? EmployeeName { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? OtherText { get; set; }
    public string? MetaUrl { get; set; }
    public string? ContactType { get; set; }
    public DateTime ClickedAt { get; set; }
}

public class OrderFollowUpRequest
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public string RequestType { get; set; } = "Complaint";
    public string? Note { get; set; }
    public string? ImagePath { get; set; }
    public string? ImageS3Key { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsClosed { get; set; }
    public string? ClosedByUserId { get; set; }
    public string? ClosedByName { get; set; }
    public DateTime? ClosedAt { get; set; }
    public string? ComplaintStatus { get; set; }
    public DateTime? ProcessingStartedAt { get; set; }
    public string? ProcessingStartedByUserId { get; set; }
    public string? ProcessingStartedByName { get; set; }
}

public class OrderDetailsFieldAuditLog
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string FieldKey { get; set; } = string.Empty;
    public string FieldLabel { get; set; } = string.Empty;
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string? ChangeReason { get; set; }
    public string? CopiedValue { get; set; }
    public string? SourcePageName { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
}

public class OrderContentViewLog
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string ContentKey { get; set; } = string.Empty;
    public string? ContentLabel { get; set; }
    public string? SourcePageName { get; set; }
    public DateTime ViewedAt { get; set; }
    public string? ViewedByUserId { get; set; }
    public string? ViewedByUserName { get; set; }
}

public class OrderContentViewReadState
{
    public long Id { get; set; }
    public int OrderId { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public string ContentKey { get; set; } = string.Empty;
    public string ReaderUserId { get; set; } = string.Empty;
    public DateTime LastReadAt { get; set; }
}

public class OrderPackagingAchievementRun
{
    public long Id { get; set; }
    public string RunKey { get; set; } = string.Empty;
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string OrderIds { get; set; } = string.Empty;
    public int OrderCount { get; set; }
    public DateTime RunStartedAt { get; set; }
    public DateTime? WorkStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int? DurationSeconds { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class OrderPackagingAchievementNotification
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? AcknowledgedAt { get; set; }
}

public enum ScheduledSendStatus { Pending = 0, Firing = 1, Completed = 2, Cancelled = 3, Failed = 4 }

public class ScheduledSendRequest
{
    public int Id { get; set; }
    public string CourierKey { get; set; } = string.Empty;
    public string? RequestedByUserId { get; set; }
    public DateTime RequestedAtUtc { get; set; }
    public int DelayMinutes { get; set; }
    public int OrderCount { get; set; }
    public DateTime FireAtUtc { get; set; }
    public ScheduledSendStatus Status { get; set; }
    public DateTime? ClaimedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string? CancelledByUserId { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? ResultSummary { get; set; }
}
