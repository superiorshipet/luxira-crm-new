using Luxira.Application.Features.SearchKeywords.GetSearchKeywordOptions;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Luxira.Api.Features.SearchKeywords.GetSearchKeywordOptions;

internal static class SearchKeywordOptionController
{
    internal static IEndpointRouteBuilder MapSearchKeywordOptionController(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/administration/search-keywords/options",
                GetOptions)
            .WithName("SearchKeywords_GetOptions")
            .WithTags("Search Keyword Administration")
            .WithSummary("Get search-keyword editor options")
            .Produces<SearchKeywordOptionsResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        endpoints.MapGet(
                "/Home/GetSearchKeywordOptions",
                GetOptions)
            .WithName("LegacyHome_GetSearchKeywordOptions")
            .WithTags("Legacy Compatibility")
            .WithSummary("Get search-keyword options using the legacy route")
            .Produces<SearchKeywordOptionsResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        return endpoints;
    }

    private static async Task<Ok<SearchKeywordOptionsResult>> GetOptions(
        GetSearchKeywordOptionsService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ExecuteAsync(cancellationToken);
        return TypedResults.Ok(result);
    }
}
