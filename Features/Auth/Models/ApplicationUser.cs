using System.ComponentModel.DataAnnotations.Schema;

namespace Luxira.Api.Features.Auth.Models;

public class ApplicationUser
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? UserName { get; set; }
    public string? NormalizedUserName { get; set; }
    public string? Email { get; set; }
    public string? NormalizedEmail { get; set; }
    public string? PasswordHash { get; set; }
    public string? SecurityStamp { get; set; }
    public string? ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString();
    public string? PhoneNumber { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool PhoneNumberConfirmed { get; set; }
    public bool TwoFactorEnabled { get; set; }
    public DateTimeOffset? LockoutEnd { get; set; }
    public bool LockoutEnabled { get; set; }
    public int AccessFailedCount { get; set; }

    public int AcessId { get; set; }
    public string? Name { get; set; }
    public int? Country { get; set; }
    public List<ApplicationUserRole> UserRoles { get; set; } = [];

    [NotMapped]
    public IReadOnlyList<string> Roles =>
        UserRoles
            .Select(userRole => userRole.Role?.Name)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Cast<string>()
            .ToArray();

    [NotMapped]
    public string? Role
    {
        get
        {
            var roles = Roles;
            return roles.Count > 0 ? roles[0] : _requestedRole;
        }
        set => _requestedRole = value;
    }

    private string? _requestedRole;

    [NotMapped]
    public bool IsActive
    {
        get => !LockoutEnd.HasValue || LockoutEnd <= DateTimeOffset.UtcNow;
        set => LockoutEnd = value ? null : DateTimeOffset.MaxValue;
    }
}

public class ApplicationRole
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string? Name { get; set; }
    public string? NormalizedName { get; set; }
    public string? ConcurrencyStamp { get; set; }
}

public class ApplicationUserRole
{
    public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public ApplicationRole? Role { get; set; }
}

public class UserSwitchGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<UserSwitchGroupMember> Members { get; set; } = new();
}

public class UserSwitchGroupMember
{
    public int Id { get; set; }
    public int UserSwitchGroupId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public UserSwitchGroup? UserSwitchGroup { get; set; }
}
