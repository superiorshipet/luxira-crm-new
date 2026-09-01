namespace Luxira.Api.Features.Auth.DTOs;

public record LoginRequest(string Username, string Password);

public record RegisterRequest(string Username, string Email, string Password, string? Name, int? Country, int AccessId = 0);

public record AuthResponse(
    string Token,
    string RefreshToken,
    DateTime ExpiresAt,
    UserDto User
);

public record UserDto(
    string Id,
    string Username,
    string? Email,
    string? Name,
    int? Country,
    int AccessId,
    string? Role,
    bool IsActive
);

public record UserProfileResponse(
    string Id,
    string Username,
    string? Email,
    string? Name,
    int? Country,
    int AccessId,
    string? Role,
    string ProfileSource
);

public record SwitchUserRequest(string TargetUserId);

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
