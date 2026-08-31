using Luxira.Api.Errors;
using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.SearchKeywords.ListSearchKeywords;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.SearchKeywords.ListSearchKeywords;

internal static class SearchKeywordController
{
    internal static IEndpointRouteBuilder MapSearchKeywordController(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/administration/search-keywords",
                ListCanonical)
            .WithName("SearchKeywords_List")
            .WithTags("Search Keyword Administration")
            .WithSummary("List and filter home search keywords")
            .Produces<SearchKeywordListResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable)
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        endpoints.MapGet(
                "/Home/GetSearchKeywords",
                ListLegacy)
            .WithName("LegacyHome_GetSearchKeywords")
            .WithTags("Legacy Compatibility")
            .WithSummary("List home search keywords using the legacy route")
            .Produces<SearchKeywordListResult>()
            .Produces<SearchKeywordFailureResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        return endpoints;
    }

    private static async Task<Results<
        Ok<SearchKeywordListResult>,
        ProblemHttpResult>> ListCanonical(
        [FromQuery] string? search,
        [FromQuery] string? targetType,
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        ListSearchKeywordsService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(
                search,
                targetType,
                category,
                isActive,
                cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (ReadStoreUnavailableException exception)
        {
            return ReadStoreProblem.Create(exception);
        }
    }

    private static async Task<Results<
        Ok<SearchKeywordListResult>,
        Ok<SearchKeywordFailureResponse>>> ListLegacy(
        [FromQuery] string? search,
        [FromQuery] string? targetType,
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        ListSearchKeywordsService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(
                search,
                targetType,
                category,
                isActive,
                cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (Exception exception)
        {
            return TypedResults.Ok(new SearchKeywordFailureResponse(
                false,
                "حدث خطأ أثناء جلب كلمات البحث: " + exception.Message));
        }
    }
}
