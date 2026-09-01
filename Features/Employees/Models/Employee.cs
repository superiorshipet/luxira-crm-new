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
    public DateTime HireDate { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

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
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string ActivityType { get; set; } = string.Empty;
    public string? Details { get; set; }
}

public class EmployeeSalaryPayment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public decimal Amount { get; set; }
    public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    public string? Notes { get; set; }
    public string PaidByUserId { get; set; } = string.Empty;
}

public class EmployeeBonusRate
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public decimal Rate { get; set; }
    public int TargetOrders { get; set; }
}

public class EmployeeBonusPayment
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public decimal Amount { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
}

public class EmployeeTask
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
}

public class EmployeeError
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal? DeductionAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class EmployeeTransaction
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public decimal Amount { get; set; }
    public string TransactionType { get; set; } = string.Empty; // Advance, Bonus, Deduction
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
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
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReminderAt { get; set; }
}

public class ManagementRequest
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string RequestType { get; set; } = "Leave"; // Leave, Advance, Expense, ShiftSwap, Resignation
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal? RequestedAmount { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string? ManagerFeedback { get; set; }
    public string? ReviewedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAt { get; set; }
}

public class ScreenRecord
{
    public int Id { get; set; }
    public string EmployeeId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public string VideoPath { get; set; } = string.Empty;
    public string? VideoS3Key { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
