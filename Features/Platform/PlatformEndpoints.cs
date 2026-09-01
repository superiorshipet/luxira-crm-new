using Luxira.Api.OpenApi;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Luxira.Api.Features.Platform;

internal static class PlatformEndpoints
{
    internal static IEndpointRouteBuilder MapPlatformEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var platform = endpoints
            .MapGroup(string.Empty)
            .WithTags("Platform")
            .AllowAnonymous();

        platform.MapGet(
                "/",
                () => TypedResults.Redirect(OpenApiExtensions.V1DocumentPath))
            .WithName("Platform_RedirectToOpenApi")
            .WithSummary("Import the project base URL directly into Postman")
            .Produces(StatusCodes.Status302Found);

        platform.MapGet(
                "/api",
                (HttpRequest request) =>
                {
                    var openApiUrl = new Uri(
                        new Uri($"{request.Scheme}://{request.Host}"),
                        OpenApiExtensions.V1DocumentPath);

                    return TypedResults.Ok(
                        new ApiDiscoveryResponse(
                            "Luxira API",
                            "v1",
                            openApiUrl.ToString(),
                            "Paste openApiUrl into Postman Import -> Link to generate the endpoint collection automatically."));
                })
            .WithName("Platform_GetApiDiscovery")
            .WithSummary("Discover the API and its Postman import URL")
            .Produces<ApiDiscoveryResponse>();

        platform.MapGet(
                "/health/live",
                () => TypedResults.Ok(
                    new HealthResponse(
                        HealthStatus.Healthy.ToString(),
                        DateTimeOffset.UtcNow)))
            .WithName("Platform_GetLiveness")
            .WithSummary("Check whether the API process is alive")
            .Produces<HealthResponse>();

        platform.MapGet(
                "/health/ready",
                async Task<Results<Ok<HealthResponse>, StatusCodeHttpResult>> (
                    HealthCheckService healthChecks,
                    CancellationToken cancellationToken) =>
                {
                    var report = await healthChecks.CheckHealthAsync(
                        cancellationToken);

                    if (report.Status != HealthStatus.Healthy)
                    {
                        return TypedResults.StatusCode(
                            StatusCodes.Status503ServiceUnavailable);
                    }

                    return TypedResults.Ok(
                        new HealthResponse(
                            report.Status.ToString(),
                            DateTimeOffset.UtcNow));
                })
            .WithName("Platform_GetReadiness")
            .WithSummary("Check whether the API is ready to receive traffic")
            .Produces<HealthResponse>()
            .Produces(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }
}

internal sealed record ApiDiscoveryResponse(
    string Name,
    string Version,
    string OpenApiUrl,
    string PostmanImportInstructions);

internal sealed record HealthResponse(
    string Status,
    DateTimeOffset CheckedAtUtc);
