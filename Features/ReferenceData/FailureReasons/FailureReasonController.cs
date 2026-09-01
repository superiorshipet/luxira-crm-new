using Microsoft.AspNetCore.Http.HttpResults;

namespace Luxira.Api.Features.ReferenceData.FailureReasons;

internal static class FailureReasonController
{
    internal static IEndpointRouteBuilder MapFailureReasonController(
        this IEndpointRouteBuilder endpoints)
    {
        var publicEndpoints = endpoints.MapGroup(string.Empty).AllowAnonymous();

        publicEndpoints.MapGet(
                "/api/v1/reference-data/failure-reasons",
                GetFailureReasons)
            .WithName("ReferenceData_GetFailureReasons")
            .WithTags("Reference Data")
            .WithSummary("List delivery failure reasons")
            .CacheOutput("ReferenceData")
            .Produces<FailureReason[]>();

        return endpoints;
    }

    private static Ok<FailureReason[]> GetFailureReasons() =>
        TypedResults.Ok(FailureReasonCatalog.All);
}
