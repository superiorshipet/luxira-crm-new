namespace Luxira.Api.Features.Orders.Models;

public class PotentialOrder
{
    public int Id { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string TelephoneNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? Notes { get; set; }
    public int Country { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? ChatUrl { get; set; }
    public string? PageName { get; set; }
    public string? PostUrl { get; set; }
    public string? AssignedUserId { get; set; }
    public bool IsConverted { get; set; }
    public int? ConvertedOrderId { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
}

public class UrgentReport
{
    public int Id { get; set; }
    public int? OrderId { get; set; }
    public Order? Order { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Priority { get; set; } = "High"; // Low, Medium, High, Critical
    public string Status { get; set; } = "Open"; // Open, InProgress, Resolved, Closed
    public string ReportedByUserId { get; set; } = string.Empty;
    public string? AssignedToUserId { get; set; }
    public string? ResolutionNote { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}
