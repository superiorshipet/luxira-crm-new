using Microsoft.AspNetCore.Http.HttpResults;

namespace Luxira.Api.Features.ReferenceData.OrderSources;

internal static class OrderSourceEndpoints
{
    internal static IEndpointRouteBuilder MapOrderSourceEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/reference-data/order-sources",
                GetOrderSources)
            .WithName("ReferenceData_GetOrderSources")
            .WithTags("Reference Data")
            .WithSummary("List order acquisition sources")
            .CacheOutput("ReferenceData")
            .Produces<OrderSourceResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        endpoints.MapGet(
                "/DataList/GetAllOrderSources",
                GetOrderSources)
            .WithName("LegacyDataList_GetAllOrderSources")
            .WithTags("Legacy Compatibility")
            .WithSummary("List order sources using the authenticated legacy route")
            .CacheOutput("ReferenceData")
            .Produces<OrderSourceResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized);

        return endpoints;
    }

    private static Ok<OrderSourceResponse[]> GetOrderSources() =>
        TypedResults.Ok(OrderSourceCatalog.All);
}
