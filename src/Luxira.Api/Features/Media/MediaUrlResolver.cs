namespace Luxira.Api.Features.Media;

internal static class MediaUrlResolver
{
    internal static string ResolveLegacyUrl(string? url)
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
