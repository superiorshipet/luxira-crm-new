using System.Security.Claims;
using Luxira.Api.Authentication;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Luxira.Api.Features.ReferenceData.OrderStatuses;

internal static class OrderStatusEndpoints
{
    internal static IEndpointRouteBuilder MapOrderStatusEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/reference-data/order-statuses",
                GetOrderStatuses)
            .WithName("ReferenceData_GetOrderStatuses")
            .WithTags("Reference Data")
            .WithSummary("List order statuses allowed for the current role")
            .RequireAuthorization(LuxiraPolicies.ReadOrderStatuses)
            .Produces<OrderStatusResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        endpoints.MapGet(
                "/DataList/GetAllOrderStatuses",
                GetOrderStatuses)
            .WithName("LegacyDataList_GetAllOrderStatuses")
            .WithTags("Legacy Compatibility")
            .WithSummary("List role-scoped order statuses using the legacy route")
            .RequireAuthorization(LuxiraPolicies.ReadOrderStatuses)
            .Produces<OrderStatusResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return endpoints;
    }

    private static Results<Ok<OrderStatusResponse[]>, ForbidHttpResult>
        GetOrderStatuses(ClaimsPrincipal user)
    {
        if (user.IsInRole("Admin") || user.IsInRole("ExecutiveDirector"))
        {
            return TypedResults.Ok(OrderStatusCatalog.Administrators);
        }

        if (user.IsInRole("DeliveryCompany") ||
            user.IsInRole("DeliveryRepresentative"))
        {
            return TypedResults.Ok(OrderStatusCatalog.Delivery);
        }

        if (user.IsInRole("CallCenter"))
        {
            return TypedResults.Ok(OrderStatusCatalog.CallCenter);
        }

        if (user.IsInRole("FollowUpDepartment"))
        {
            return TypedResults.Ok(OrderStatusCatalog.FollowUp);
        }

        return TypedResults.Forbid();
    }
}
