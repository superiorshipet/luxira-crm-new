namespace Luxira.Application.Features.Identity.GetUserProfile;

public sealed record UserProfileResult(
    string Id,
    string Name,
    string Avatar,
    string Role,
    string Title,
    string Phone);
