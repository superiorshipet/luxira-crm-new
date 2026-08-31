namespace Luxira.Application.Features.DeliveryCompanies;

internal static class DeliveryMediaUrl
{
    internal static string Resolve(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "/static/DefaultImage.svg";
        }

        return url.StartsWith('/') ||
            url.StartsWith("http://", StringComparison.Ordinal) ||
            url.StartsWith("https://", StringComparison.Ordinal)
                ? url
                : "/" + url;
    }
}
