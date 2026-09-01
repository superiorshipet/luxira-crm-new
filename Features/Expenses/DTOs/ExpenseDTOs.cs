namespace Luxira.Api.Features.Expenses.DTOs;

public record ExpenseDto(
    int Id,
    string Description,
    decimal Amount,
    int Country,
    string? Category,
    DateTime Date,
    string CreatedByUserId,
    string? AttachmentUrl,
    string? Notes
);

public record CreateExpenseRequest(
    string Description,
    decimal Amount,
    int Country,
    string? Category,
    DateTime? Date,
    string? AttachmentUrl,
    string? Notes
);

public record ExpenseFilterRequest(
    int? Country,
    string? Category,
    DateTime? FromDate,
    DateTime? ToDate
);

public record ExchangeRateDto(
    int Id,
    string FromCurrency,
    string ToCurrency,
    decimal Rate,
    DateTime UpdatedAt
);

public record UpdateExchangeRateRequest(
    string FromCurrency,
    string ToCurrency,
    decimal Rate
);
