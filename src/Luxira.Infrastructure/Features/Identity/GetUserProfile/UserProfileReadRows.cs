namespace Luxira.Infrastructure.Features.Identity.GetUserProfile;

internal sealed class UserProfileUserReadRow
{
    internal required string Id { get; init; }
    internal string? UserName { get; init; }
    internal string? Email { get; init; }
    internal string? DisplayName { get; init; }
    internal string? PhoneNumber { get; init; }
}

internal sealed class EmployeeProfileReadRow
{
    internal int Id { get; init; }
    internal required string ApplicationUserId { get; init; }
    internal bool IsActive { get; init; }
    internal string? DisplayName { get; init; }
    internal string? Name { get; init; }
    internal string? ImageUrl { get; init; }
    internal string? JobTitle { get; init; }
    internal string? PhoneNumber { get; init; }
}

internal sealed class UserRoleReadRow
{
    internal required string UserId { get; init; }
    internal required string RoleId { get; init; }
}

internal sealed class RoleReadRow
{
    internal required string Id { get; init; }
    internal string? Name { get; init; }
}
