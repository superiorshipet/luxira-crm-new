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
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByName { get; set; }
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
    public DateTime? LastActivityAt { get; set; }
    public string? CurrentPage { get; set; }
    public bool IsTabActive { get; set; }
    public int TotalOnlineSeconds { get; set; }
    public int TotalActiveSeconds { get; set; }
    public DateTime? LastHeartbeatAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
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
    public string EmployeeId { get; set; } = string.Empty;
    public decimal ProBonusPercentage { get; set; }
    public decimal ProThreshold { get; set; }
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
    public string EmployeeId { get; set; } = string.Empty;
    public ApplicationUser? Employee { get; set; }
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
    public string TaskCategory { get; set; } = "Normal";
    public string? TargetPageKey { get; set; }
    public string? TargetPageName { get; set; }
    public string? TargetPageUrl { get; set; }
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

public class SystemDevelopmentTask
{
    public int Id { get; set; } public string Title { get; set; } = string.Empty; public string? Description { get; set; } public byte Category { get; set; } = 1; public byte? PreviousCategory { get; set; } public int SortOrder { get; set; } public bool IsCompleted { get; set; } public bool IsDeleted { get; set; }
    public string? CreatedByUserId { get; set; } public string? CreatedByName { get; set; } public DateTime CreatedAt { get; set; } public string? UpdatedByUserId { get; set; } public string? UpdatedByName { get; set; } public DateTime? UpdatedAt { get; set; } public string? DeletedByUserId { get; set; } public string? DeletedByName { get; set; } public DateTime? DeletedAt { get; set; } public byte[] RowVersion { get; set; } = [];
    public int? EstimatedMinutes { get; set; } public bool IsPinned { get; set; } public DateTime? PinnedAt { get; set; }
    public ICollection<SystemDevelopmentTaskImage> Images { get; set; } = [];
}
public class SystemDevelopmentTaskImage { public int Id { get; set; } public int DevelopmentTaskId { get; set; } public SystemDevelopmentTask? Task { get; set; } public string ImageUrl { get; set; } = string.Empty; public string? OriginalFileName { get; set; } public int SortOrder { get; set; } public DateTime CreatedAt { get; set; } }
public class SystemDevelopmentTaskAuditLog { public long Id { get; set; } public int? DevelopmentTaskId { get; set; } public string TaskTitle { get; set; } = string.Empty; public string ActionType { get; set; } = string.Empty; public string ActionText { get; set; } = string.Empty; public string? OldDataJson { get; set; } public string? NewDataJson { get; set; } public string? ChangedByUserId { get; set; } public string? ChangedByName { get; set; } public DateTime ChangedAt { get; set; } }
public class DevelopmentTaskAssignment { public int Id { get; set; } public int TaskId { get; set; } public int EmployeeId { get; set; } public string EmployeeName { get; set; } = string.Empty; public DateTimeOffset AssignedAt { get; set; } public string? AssignedByUserId { get; set; } public string? AssignedByName { get; set; } public byte DeveloperStatus { get; set; } public DateTimeOffset? StartedAt { get; set; } public bool TimerStartedManually { get; set; } public DateTimeOffset? CompletedAt { get; set; } }
public class DevelopmentTaskComment { public long Id { get; set; } public int TaskId { get; set; } public int EmployeeId { get; set; } public string EmployeeName { get; set; } = string.Empty; public string CommentText { get; set; } = string.Empty; public DateTimeOffset CreatedAt { get; set; } }
public class MarketingWorkReport { public long Id { get; set; } public int EmployeeId { get; set; } public string EmployeeName { get; set; } = string.Empty; public bool IsCompleted { get; set; } public string ReportText { get; set; } = string.Empty; public DateTimeOffset CreatedAt { get; set; } }
public class DevelopmentTaskReviewSubmission { public int Id { get; set; } public int TaskId { get; set; } public int EmployeeId { get; set; } public string? OriginalFileName { get; set; } public string? StoredFileName { get; set; } public string? FilePath { get; set; } public string? ContentType { get; set; } public long? FileSize { get; set; } public DateTimeOffset SubmittedAt { get; set; } public string? SubmissionType { get; set; } public string? AccomplishedText { get; set; } public string? NotAccomplishedText { get; set; } public ICollection<DevelopmentTaskReviewFile> Files { get; set; } = []; }
public class DevelopmentTaskReviewFile { public int Id { get; set; } public int SubmissionId { get; set; } public DevelopmentTaskReviewSubmission? Submission { get; set; } public string OriginalFileName { get; set; } = string.Empty; public string StoredFileName { get; set; } = string.Empty; public string FilePath { get; set; } = string.Empty; public string? ContentType { get; set; } public long FileSize { get; set; } public int SortOrder { get; set; } public DateTimeOffset CreatedAt { get; set; } }

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
    public string? EmployeeNameSnapshot { get; set; }
    public string? ChatType { get; set; }
    public string? ImageUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedByUserId { get; set; }
    public string? CreatedByUserName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByName { get; set; }
    public string? PageUrl { get; set; }
    public string? LinkedOrderPostIds { get; set; }
    public string? EmployeeReason { get; set; }
    public int ErrorCount { get; set; }
    public bool IsAcknowledged { get; set; }
    public bool IsReasonProvided { get; set; }
    public int SeverityLevel { get; set; }
}

public class EmployeeErrorEditHistory
{
    public int Id { get; set; }
    public int EmployeeErrorId { get; set; }
    public int EmployeeId { get; set; }
    public string? EmployeeNameSnapshot { get; set; }
    public string? OldPageUrl { get; set; }
    public string? NewPageUrl { get; set; }
    public string? OldChatType { get; set; }
    public string? NewChatType { get; set; }
    public string? OldErrorText { get; set; }
    public string? NewErrorText { get; set; }
    public string? OldImageUrl { get; set; }
    public string? NewImageUrl { get; set; }
    public DateTime? EditedAt { get; set; }
    public string? EditedByUserId { get; set; }
    public string? EditedByName { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? EditedByUserName { get; set; }
}

public class EmployeeTransaction
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public decimal Amount { get; set; }
    public EmployeeTransactionType TransactionType { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Reason { get; set; }
    public int? EmployeePaymentSummaryId { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserName { get; set; }
    public string? EditHistoryJson { get; set; }
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
    public long Id { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string HtmlContent { get; set; } = string.Empty;
    public string PlainText { get; set; } = string.Empty;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}

public class PersonalNoteHistory
{
    public long Id { get; set; }
    public long PersonalNoteId { get; set; }
    public PersonalNote? PersonalNote { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string PreviousHtmlContent { get; set; } = string.Empty;
    public string NewHtmlContent { get; set; } = string.Empty;
    public string PreviousPlainText { get; set; } = string.Empty;
    public string NewPlainText { get; set; } = string.Empty;
    public DateTime ChangedAt { get; set; }
    public string ChangedByUserId { get; set; } = string.Empty;
    public string ChangedByName { get; set; } = string.Empty;
}

public class IdeaSuggestion
{
    public long Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeImage { get; set; }
    public string IdeaText { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? AdminAcknowledgedAtUtc { get; set; }
}

public class ManagementRequest
{
    public long Id { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string EmployeeName { get; set; } = string.Empty;
    public string? EmployeeEmail { get; set; }
    public string RequestType { get; set; } = "Leave"; // Leave, Advance, Expense, ShiftSwap, Resignation
    public string Reason { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public string? DecidedByUserId { get; set; }
    public string? DecidedByName { get; set; }
}

public class ManagementRequestNotification
{
    public long Id { get; set; }
    public long ManagementRequestId { get; set; }
    public ManagementRequest? ManagementRequest { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}

public enum EmployeeTransactionType
{
    Deduction = 0,
    Bonus = 1,
    Advance = 2,
    Overtime = 3,
}

public class DevelopmentTaskCategoryAssignmentRule
{
    public int Category { get; set; }
    public int EmployeeId { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
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
