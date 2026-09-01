namespace Luxira.Api.Features.Employees.DTOs;

public record EmployeeDto(
    int Id,
    string Name,
    string? DisplayName,
    string IdNumber,
    string Nationality,
    string? Country,
    string PhoneNumber,
    string Address,
    decimal Salary,
    string? JobTitle,
    DateTime HireDate,
    bool IsActive,
    string? ImageUrl,
    string? ApplicationUserId
);

public record CreateEmployeeRequest(
    string Name,
    string? DisplayName,
    string IdNumber,
    string Nationality,
    string? Country,
    string PhoneNumber,
    string Address,
    decimal Salary,
    string? JobTitle,
    string? ApplicationUserId
);

public record UpdateEmployeeRequest(
    string? Name,
    string? DisplayName,
    string? PhoneNumber,
    string? Address,
    decimal? Salary,
    string? JobTitle,
    bool? IsActive
);

public record AttendanceLogDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    DateTime CheckIn,
    DateTime? CheckOut,
    string? Note
);

public record CheckInRequest(
    int EmployeeId,
    string? Note
);

public record CheckOutRequest(
    int AttendanceLogId,
    string? Note
);

public record SalaryPaymentDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    decimal Amount,
    DateTime PaymentDate,
    string? Notes
);

public record RecordSalaryPaymentRequest(
    int EmployeeId,
    decimal Amount,
    string? Notes
);
