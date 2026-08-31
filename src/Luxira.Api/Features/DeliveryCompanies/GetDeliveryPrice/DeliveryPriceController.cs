using Luxira.Api.Errors;
using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.DeliveryCompanies.GetDeliveryPrice;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.GetDeliveryPrice;

internal static class DeliveryPriceController
{
    internal static IEndpointRouteBuilder MapDeliveryPriceController(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/delivery-companies/{deliveryCompanyId:int}/price",
                GetDeliveryPrice)
            .WithName("DeliveryCompanies_GetPrice")
            .WithTags("Delivery Companies")
            .WithSummary("Get the most specific delivery price")
            .Produces<DeliveryPriceResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/DataList/GetDeliveryPrice",
                GetDeliveryPrice)
            .WithName("LegacyDataList_GetDeliveryPrice")
            .WithTags("Legacy Compatibility")
            .WithSummary("Get a delivery price using the legacy route")
            .Produces<DeliveryPriceResult>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<
        Ok<DeliveryPriceResult>,
        ProblemHttpResult>> GetDeliveryPrice(
        int deliveryCompanyId,
        [FromQuery(Name = "countryId")] int countryId,
        [FromQuery(Name = "cityId")] string? cityId,
        GetDeliveryPriceService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(
                deliveryCompanyId,
                countryId,
                cityId,
                cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (ReadStoreUnavailableException exception)
        {
            return ReadStoreProblem.Create(exception);
        }
    }
}
