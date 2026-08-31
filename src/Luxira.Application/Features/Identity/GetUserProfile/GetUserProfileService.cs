namespace Luxira.Application.Features.Identity.GetUserProfile;

public sealed class GetUserProfileService(IGetUserProfileRepository repository)
{
    public async Task<UserProfileResult?> ExecuteAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var source = await repository.GetAsync(userId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        var name = FirstNonblank(
            source.EmployeeDisplayName,
            source.EmployeeName,
            source.UserDisplayName,
            source.UserName,
            source.Email) ?? "موظف";
        var role = FirstNonblank(
            source.EmployeeJobTitle,
            source.FirstRole) ?? "مستخدم";
        var phone = FirstNonblank(
            source.EmployeePhoneNumber,
            source.UserPhoneNumber) ?? "-";

        return new UserProfileResult(
            source.Id,
            name,
            ResolveAvatar(source.Id, source.EmployeeImageUrl),
            role,
            role,
            phone);
    }

    private static string ResolveAvatar(string userId, string? imageUrl)
    {
        if (string.IsNullOrWhiteSpace(imageUrl))
        {
            return "/Conference/Avatar?id=" + userId;
        }

        var url = imageUrl.Trim().Replace('\\', '/');
        if (url.StartsWith('~'))
        {
            url = url[1..];
        }

        return url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
            url.StartsWith('/')
                ? url
                : "/" + url;
    }

    private static string? FirstNonblank(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
