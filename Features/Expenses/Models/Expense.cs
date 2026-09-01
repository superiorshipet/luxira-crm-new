namespace Luxira.Api.Features.Expenses.Models;

public class Expense
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class ExchangeRate
{
    public int Id { get; set; }
    public int Country { get; set; }
    public decimal BuyToUSD { get; set; }
    public decimal SellToUSD { get; set; }
}

public class SalesIndicator
{
    public int Id { get; set; }
    public int Country { get; set; }
    public int MainWarehouseId { get; set; }
    public int Quantity { get; set; }
    public decimal MinimumSellingFrom { get; set; }
    public decimal MinimumSellingTo { get; set; }
    public decimal BasicSellingFrom { get; set; }
    public decimal BasicSellingTo { get; set; }
    public decimal MiddleSellingFrom { get; set; }
    public decimal MiddleSellingTo { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? UpdatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
