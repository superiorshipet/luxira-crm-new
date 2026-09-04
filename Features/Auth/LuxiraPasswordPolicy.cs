namespace Luxira.Api.Features.Auth;

public static class LuxiraPasswordPolicy
{
    public const int MinimumLength = 6;

    public static bool IsValid(string? password) =>
        !string.IsNullOrWhiteSpace(password) &&
        password.Length >= MinimumLength &&
        password.Any(char.IsDigit);

    public const string ErrorMessage = "Password must be at least 6 characters and contain a digit.";
}
