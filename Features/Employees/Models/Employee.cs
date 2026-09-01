using Luxira.Api.Features.Auth.Models;

namespace Luxira.Api.Features.Employees.Models;

public class Employee
{
    public int Id { get; set; }
    public string? Cv { get; set; }
    public string? CvS3Key { get; set; }
    public string? ImageUrl { get; set; }
    public string? ImageS3Key { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string IdNumber { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? Country { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public decimal Salary { get; set; }
    public string? JobTitle { get; set; }
    public DateTime DateAdded { get; set; } = DateTime.UtcNow;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime HireDate { get => DateAdded; set => DateAdded = value; }
    public bool IsActive { get; set; } = true;
    public bool IsShown { get; set; } = true;
    public bool AllowMobileOrTabletLogin { get; set; }
    public bool ApplyShiftAccess { get; set; } = true;
    public bool? AllowScreenRecording { get; set; }
    public bool IsNotificationCenterBlocked { get; set; }
    public bool CanHandleUrgentReports { get; set; }
    public bool EnableOrderPackaging { get; set; }
    public TimeSpan? OrderPackagingNotificationTime { get; set; }
    public int? OrderPackagingDeliveryCompanyId { get; set; }
    public string? OrderPackagingDeliveryCompanyIds { get; set; }
    public int OrderPackagingStartGraceMinutes { get; set; } = 20;
    public DateTime? OrderPackagingTriggeredAt { get; set; }
    public DateTime? OrderPackagingFirstPreparedAt { get; set; }
    public DateTime? OrderPackagingCompletedAt { get; set; }
    public DateTime? OrderPackagingLastEmployeeReminderAt { get; set; }
    public DateTime? OrderPackagingLastAdminReminderAt { get; set; }
    public DateTime? OrderPackagingCompletionNotifiedAt { get; set; }
    public string? FaceDescriptor { get; set; }
    public bool HasFacePrint { get; set; }

    public string? ApplicationUserId { get; set; }
    public ApplicationUser? ApplicationUser { get; set; }

    public List<EmployeeAttendanceLog> AttendanceLogs { get; set; } = new();
    public List<EmployeeSalaryPayment> SalaryPayments { get; set; } = new();
}

public class EmployeeAttendanceLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string? EmployeeEmail { get; set; }
    public string? EmployeeName { get; set; }
    public DateTime CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }
    public string? FaceImagePath { get; set; }
    public string? FaceImageS3Key { get; set; }
    public string? CheckOutFaceImagePath { get; set; }
    public string? CheckOutFaceImageS3Key { get; set; }
    public string? CheckInIpAddress { get; set; }
    public string? CheckInLocation { get; set; }
    public string? CheckOutIpAddress { get; set; }
    public string? CheckOutLocation { get; set; }
    public decimal? SalaryAtCheckIn { get; set; }
    public decimal? DeductionAmount { get; set; }
    public string? DeductionReason { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int? ShiftId { get; set; }
    public DateTime? ShiftStartAt { get; set; }
    public DateTime? ShiftEndAt { get; set; }
    public DateTime? BreakStartAt { get; set; }
}

public class EmployeeWorkShift
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public TimeSpan ShiftStartTime { get; set; }
    public TimeSpan ShiftEndTime { get; set; }
    public string? AllowedIpAddress { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsLoginBlocked { get; set; }
    public DateTime? LoginBlockedAt { get; set; }
    public string? LoginBlockReason { get; set; }
    public DateTime? AdminUnblockedUntil { get; set; }
    public DateTime? AdminUnblockedAt { get; set; }
    public string? AdminUnblockedByUserId { get; set; }
    public int BreakDurationMinutes { get; set; } = 30;
    public TimeSpan? ScheduledBreakStart { get; set; }
    public TimeSpan? ScheduledBreakEnd { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class EmployeeActivityLog
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string? EmployeeName { get; set; }
    public string? EmployeeEmail { get; set; }
    public string? EmployeeImageUrl { get; set; }
    public DateTime ActivityDate { get; set; }
    public DateTime FirstSeenAt { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime LastActivityAt { get; set; }
    public string? CurrentPage { get; set; }
    public bool IsTabActive { get; set; }
    public int TotalOnlineSeconds { get; set; }
    public int TotalActiveSeconds { get; set; }
    public DateTime LastHeartbeatAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class EmployeeSalaryPayment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime SalaryMonth { get; set; }
    public DateTime PeriodFrom { get; set; }
    public DateTime PeriodTo { get; set; }
    public decimal MonthlySalary { get; set; }
    public int DaysWorked { get; set; }
    public int DaysInMonth { get; set; }
    public decimal EarnedSalary { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalAdvances { get; set; }
    public decimal TotalBonuses { get; set; }
    public decimal RemainingAmount { get; set; }
    public decimal ManualAdjustmentAmount { get; set; }
    public string? ManualAdjustmentReason { get; set; }
    public DateTime? ManualAdjustmentAt { get; set; }
    public string? ManualAdjustmentByUserId { get; set; }
    public string? ManualAdjustmentByUserName { get; set; }
    public string Currency { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public DateTime? PaidAt { get; set; }
    public string? PaidByUserId { get; set; }
    public string? PaidByUserName { get; set; }
    public string ReceiptNumber { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByUserName { get; set; }
    public bool IsPermanentlyDeleted { get; set; }
    public DateTime? PermanentlyDeletedAt { get; set; }
    public string? PermanentlyDeletedByUserId { get; set; }
    public string? PermanentlyDeletedByUserName { get; set; }
}

public class EmployeeBonusRate
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public decimal ProBonusPercentage { get; set; }
    public int ProThreshold { get; set; }
    public bool IsBonusPanelHidden { get; set; }
    public bool IsBonusAmountsRevealed { get; set; }
    public decimal BonusPercentage { get; set; }
    public decimal BonusProcessingPercentage { get; set; }
    public decimal ProBonusProcessingPercentage { get; set; }
    public decimal MinimumBonusThreshold { get; set; }
}

public class EmployeeBonusPayment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime DatePaid { get; set; } = DateTime.UtcNow;
    public decimal AmountPaid { get; set; }
    public decimal ProExtraAmount { get; set; }
    public int TotalOrderCount { get; set; }
    public int ProOrderCount { get; set; }
    public int SuccessOrderCount { get; set; }
    public int ProcessingOrderCount { get; set; }
    public decimal ProcessingAmount { get; set; }
    public decimal SuccessAmount { get; set; }
}

public class EmployeeTask
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public string Priority { get; set; } = "Important";
    public string? AttachmentUrl { get; set; }
    public string? AttachmentS3Key { get; set; }
    public string? AttachmentType { get; set; }
    public string? AttachmentImagesJson { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public ICollection<EmployeeTaskAssignment> Assignments { get; set; } = new List<EmployeeTaskAssignment>();
}

public class EmployeeTaskAssignment
{
    public int Id { get; set; }
    public int EmployeeTaskId { get; set; }
    public EmployeeTask? EmployeeTask { get; set; }
    public string EmployeeUserId { get; set; } = string.Empty;
    public string? EmployeeName { get; set; }
    public string? EmployeeImageUrl { get; set; }
    public DateTime AssignedAt { get; set; }
    public DateTime? SeenAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? DueAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletionNote { get; set; }
    public DateTime? DueSoonNotifiedAt { get; set; }
    public string Status { get; set; } = "New";
}

public class EmployeeError
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string ErrorText { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public string? PageUrl { get; set; }
    public string? EmployeeReason { get; set; }
    public int ErrorCount { get; set; }
    public bool IsAcknowledged { get; set; }
    public bool IsReasonProvided { get; set; }
    public int SeverityLevel { get; set; }
}

public class EmployeeTransaction
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = string.Empty; // Advance, Bonus, Deduction
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
    public int? EmployeePaymentSummaryId { get; set; }
    public bool IsDeleted { get; set; }
}

public class EmployeeViolation
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal PenaltyAmount { get; set; }
    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;
    public string IssuedByUserId { get; set; } = string.Empty;
}

public class EmployeeRating
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public int Score { get; set; } // 1 to 5 or 1 to 100
    public string? Feedback { get; set; }
    public string RatedByUserId { get; set; } = string.Empty;
    public DateTime RatedAt { get; set; } = DateTime.UtcNow;
}

public class PersonalNote
{
    public int Id { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

public class ManagementRequest
{
    public int Id { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public string RequestType { get; set; } = "Leave"; // Leave, Advance, Expense, ShiftSwap, Resignation
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public string? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
}

public class ScreenRecord
{
    public int Id { get; set; }
    public string? EmployeeId { get; set; }  // nullable in live DB
    public DateTime Date { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string VideoPath { get; set; } = string.Empty;
    public string? VideoS3Key { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
