using System.Security.Claims;
using Luxira.Api.Errors;
using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.ListDeliveryOptions;

internal static class DeliveryOptionController
{
    internal static IEndpointRouteBuilder MapDeliveryOptionController(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/delivery-options",
                ListDeliveryOptions)
            .WithName("DeliveryOptions_List")
            .WithTags("Delivery Companies")
            .WithSummary("List visible companies and representatives")
            .Produces<DeliveryOptionResult[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/DataList/GetAllDeliveryCompaniesAndRepresentatives",
                ListDeliveryOptions)
            .WithName("LegacyDataList_GetAllDeliveryCompaniesAndRepresentatives")
            .WithTags("Legacy Compatibility")
            .WithSummary("List delivery options using the legacy route")
            .Produces<DeliveryOptionResult[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<
        Ok<IReadOnlyList<DeliveryOptionResult>>,
        ProblemHttpResult>> ListDeliveryOptions(
        [FromQuery(Name = "countryId")] int? countryId,
        [FromQuery(Name = "cityId")] string? cityId,
        [FromQuery(Name = "orderId")] int? orderId,
        ClaimsPrincipal user,
        ListDeliveryOptionsService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(
                countryId,
                cityId,
                orderId,
                user.IsInRole("CallCenter"),
                cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (ReadStoreUnavailableException exception)
        {
            return ReadStoreProblem.Create(exception);
        }
    }
}
