namespace Luxira.Application.Features.Identity.GetUserProfile;

public sealed record UserProfileSource(
    string Id,
    string? UserName,
    string? Email,
    string? UserDisplayName,
    string? UserPhoneNumber,
    string? EmployeeDisplayName,
    string? EmployeeName,
    string? EmployeeImageUrl,
    string? EmployeeJobTitle,
    string? EmployeePhoneNumber,
    string? FirstRole);
