namespace Luxira.Api.Features.Orders.Models;

public class PotentialOrder
{
    public int Id { get; set; }
    public string? CustomerName { get; set; }
    public string? PhoneNumber { get; set; }
    public int Country { get; set; }
    public string? ChatUrl { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public int Status { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public DateTime? LastEditedDate { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
    public int OrderSource { get; set; }
}

public class UrgentReport
{
    public int Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ScreenshotPath { get; set; }
    public string? ScreenshotS3Key { get; set; }
    public int EmployeeId { get; set; }
    public Luxira.Api.Features.Employees.Models.Employee? Employee { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? HandledByAdminName { get; set; }
    public DateTime? HandledAt { get; set; }
}
