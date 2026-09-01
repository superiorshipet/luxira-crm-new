namespace Luxira.Api.Features.Expenses.DTOs;

public record ExpenseDto(
    int Id,
    string Description,
    decimal Amount,
    DateTime CreatedDate
);

public record CreateExpenseRequest(
    string Description,
    decimal Amount,
    DateTime? CreatedDate
);

public record ExpenseFilterRequest(
    DateTime? FromDate,
    DateTime? ToDate
);

public record ExchangeRateDto(
    int Id,
    int Country,
    decimal BuyToUSD,
    decimal SellToUSD
);

public record UpdateExchangeRateRequest(
    int Country,
    decimal BuyToUSD,
    decimal SellToUSD
);
