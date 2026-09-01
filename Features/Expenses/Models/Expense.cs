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

public class Invoice
{
    public int Id { get; set; }
    public string InvoiceNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public int Country { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? PdfUrl { get; set; }
}

public class FinancialTransfer
{
    public int Id { get; set; }
    public string FromAccount { get; set; } = string.Empty;
    public string ToAccount { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "IQD";
    public decimal ExchangeRate { get; set; } = 1.0m;
    public string TransferredByUserId { get; set; } = string.Empty;
    public DateTime TransferredAt { get; set; } = DateTime.UtcNow;
    public string? Note { get; set; }
}

