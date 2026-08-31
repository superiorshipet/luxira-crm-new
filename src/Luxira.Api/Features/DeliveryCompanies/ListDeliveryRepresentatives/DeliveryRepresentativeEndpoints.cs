using Luxira.Api.Features.Media;
using Luxira.Infrastructure.DeliveryCompanies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.ListDeliveryRepresentatives;

internal static class DeliveryRepresentativeEndpoints
{
    internal static IEndpointRouteBuilder MapDeliveryRepresentativeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/delivery-representatives",
                ListDeliveryRepresentatives)
            .WithName("DeliveryRepresentatives_List")
            .WithTags("Delivery Companies")
            .WithSummary("List visible delivery representatives")
            .Produces<DeliveryRepresentativeResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/DataList/GetAllDeliveryRepresentatives",
                ListDeliveryRepresentatives)
            .WithName("LegacyDataList_GetAllDeliveryRepresentatives")
            .WithTags("Legacy Compatibility")
            .WithSummary("List visible delivery representatives using the legacy route")
            .Produces<DeliveryRepresentativeResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<
        Ok<DeliveryRepresentativeResponse[]>,
        ProblemHttpResult>> ListDeliveryRepresentatives(
        [FromQuery(Name = "countryIds")] int[]? countryIds,
        [FromQuery(Name = "cityIds")] string[]? cityIds,
        IDeliveryCompanyReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            var representatives = await reader.ListRepresentativesAsync(
                countryIds,
                cityIds,
                cancellationToken);
            return TypedResults.Ok(
                representatives
                    .Select(representative => new DeliveryRepresentativeResponse(
                        representative.Id,
                        representative.Name,
                        MediaUrlResolver.ResolveLegacyUrl(representative.LogoUrl)))
                    .ToArray());
        }
        catch (ReadInfrastructureUnavailableException exception)
        {
            return TypedResults.Problem(
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Read infrastructure unavailable",
                detail: exception.Message);
        }
    }
}

internal sealed record DeliveryRepresentativeResponse(
    int Id,
    string Name,
    string LogoUrl);
