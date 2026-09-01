using Luxira.Api.Features.ManufacturingCompanies.Models;

namespace Luxira.Api.Features.Communication.Models;

public class PasswordEmail
{
    public int Id { get; set; }
    public int? ManufacturingCompanyId { get; set; }
    public ManufacturingCompany? ManufacturingCompany { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string? PageStatusName { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedByUserId { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedByUserId { get; set; }
    public string? UpdatedByName { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
    public string? DeletedByName { get; set; }
    public ICollection<PasswordEmailHistory> Histories { get; set; } = new List<PasswordEmailHistory>();
}

public class PasswordEmailHistory
{
    public int Id { get; set; }
    public int PasswordEmailId { get; set; }
    public PasswordEmail? PasswordEmail { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public string? OldEmail { get; set; }
    public string? NewEmail { get; set; }
    public string? OldPassword { get; set; }
    public string? NewPassword { get; set; }
    public string? OldPhoneNumber { get; set; }
    public string? NewPhoneNumber { get; set; }
    public string? OldPageStatusName { get; set; }
    public string? NewPageStatusName { get; set; }
    public DateTime ChangedAt { get; set; }
    public string? ChangedByUserId { get; set; }
    public string? ChangedByName { get; set; }
}
