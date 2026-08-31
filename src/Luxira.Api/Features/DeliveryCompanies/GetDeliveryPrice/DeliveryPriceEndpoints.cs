using Luxira.Infrastructure.DeliveryCompanies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.GetDeliveryPrice;

internal static class DeliveryPriceEndpoints
{
    internal static IEndpointRouteBuilder MapDeliveryPriceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/delivery-companies/{deliveryCompanyId:int}/price",
                GetDeliveryPrice)
            .WithName("DeliveryCompanies_GetPrice")
            .WithTags("Delivery Companies")
            .WithSummary("Get the most specific delivery price")
            .Produces<DeliveryPriceResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/DataList/GetDeliveryPrice",
                GetDeliveryPrice)
            .WithName("LegacyDataList_GetDeliveryPrice")
            .WithTags("Legacy Compatibility")
            .WithSummary("Get a delivery price using the legacy route")
            .Produces<DeliveryPriceResponse>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<
        Ok<DeliveryPriceResponse>,
        ProblemHttpResult>> GetDeliveryPrice(
        int deliveryCompanyId,
        [FromQuery(Name = "countryId")] int countryId,
        [FromQuery(Name = "cityId")] string? cityId,
        IDeliveryPriceReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            var price = await reader.GetPriceAsync(
                deliveryCompanyId,
                countryId,
                cityId,
                cancellationToken);
            return TypedResults.Ok(new DeliveryPriceResponse(price));
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

internal sealed record DeliveryPriceResponse(decimal Price);
