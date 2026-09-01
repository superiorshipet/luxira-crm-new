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
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public DateTime CheckIn { get; set; }
    public DateTime? CheckOut { get; set; }
    public string? IpAddress { get; set; }
    public string? Note { get; set; }
}

public class EmployeeWorkShift
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public Employee? Employee { get; set; }
    public string ShiftName { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int DayOfWeek { get; set; }
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
