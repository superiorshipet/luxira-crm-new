using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryRepresentatives;
using Luxira.Api.Errors;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.ListDeliveryRepresentatives;

internal static class DeliveryRepresentativeController
{
    internal static IEndpointRouteBuilder MapDeliveryRepresentativeController(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/delivery-representatives",
                ListDeliveryRepresentatives)
            .WithName("DeliveryRepresentatives_List")
            .WithTags("Delivery Companies")
            .WithSummary("List visible delivery representatives")
            .Produces<DeliveryRepresentativeResult[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/DataList/GetAllDeliveryRepresentatives",
                ListDeliveryRepresentatives)
            .WithName("LegacyDataList_GetAllDeliveryRepresentatives")
            .WithTags("Legacy Compatibility")
            .WithSummary("List visible delivery representatives using the legacy route")
            .Produces<DeliveryRepresentativeResult[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<
        Ok<IReadOnlyList<DeliveryRepresentativeResult>>,
        ProblemHttpResult>> ListDeliveryRepresentatives(
        [FromQuery(Name = "countryIds")] int[]? countryIds,
        [FromQuery(Name = "cityIds")] string[]? cityIds,
        ListDeliveryRepresentativesService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(
                countryIds,
                cityIds,
                cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (ReadStoreUnavailableException exception)
        {
            return ReadStoreProblem.Create(exception);
        }
    }
}
