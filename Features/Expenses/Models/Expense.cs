namespace Luxira.Api.Features.Expenses.Models;

public class Expense
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public int Country { get; set; }
    public string? Category { get; set; }
    public DateTime Date { get; set; } = DateTime.UtcNow;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string? AttachmentUrl { get; set; }
    public string? Notes { get; set; }
}

public class ExchangeRate
{
    public int Id { get; set; }
    public string FromCurrency { get; set; } = "USD";
    public string ToCurrency { get; set; } = "TRY";
    public decimal Rate { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class SalesIndicator
{
    public int Id { get; set; }
    public int Country { get; set; }
    public decimal TargetAmount { get; set; }
    public int Month { get; set; }
    public int Year { get; set; }
}

