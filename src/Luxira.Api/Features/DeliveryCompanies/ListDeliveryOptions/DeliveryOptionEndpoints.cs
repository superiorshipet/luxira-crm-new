using System.Security.Claims;
using Luxira.Api.Features.Media;
using Luxira.Infrastructure.DeliveryCompanies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.ListDeliveryOptions;

internal static class DeliveryOptionEndpoints
{
    internal static IEndpointRouteBuilder MapDeliveryOptionEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/delivery-options",
                ListDeliveryOptions)
            .WithName("DeliveryOptions_List")
            .WithTags("Delivery Companies")
            .WithSummary("List visible companies and representatives")
            .Produces<DeliveryOptionResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/DataList/GetAllDeliveryCompaniesAndRepresentatives",
                ListDeliveryOptions)
            .WithName("LegacyDataList_GetAllDeliveryCompaniesAndRepresentatives")
            .WithTags("Legacy Compatibility")
            .WithSummary("List delivery options using the legacy route")
            .Produces<DeliveryOptionResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<
        Ok<DeliveryOptionResponse[]>,
        ProblemHttpResult>> ListDeliveryOptions(
        [FromQuery(Name = "countryId")] int? countryId,
        [FromQuery(Name = "cityId")] string? cityId,
        [FromQuery(Name = "orderId")] int? orderId,
        ClaimsPrincipal user,
        IDeliveryCompanyReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            int? assignedCompanyId = null;
            if (user.IsInRole("CallCenter") && orderId.HasValue)
            {
                assignedCompanyId = await reader.GetAssignedCompanyIdForOrderAsync(
                    orderId.Value,
                    cancellationToken);
                if (!assignedCompanyId.HasValue)
                {
                    return TypedResults.Ok(Array.Empty<DeliveryOptionResponse>());
                }
            }

            var options = await reader.ListCompaniesAndRepresentativesAsync(
                countryId,
                cityId,
                assignedCompanyId,
                cancellationToken);
            return TypedResults.Ok(options
                .Select(option => new DeliveryOptionResponse(
                    option.Id,
                    option.Name,
                    MediaUrlResolver.ResolveLegacyUrl(option.LogoUrl),
                    option.IsRepresentative))
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

internal sealed record DeliveryOptionResponse(
    int Id,
    string Name,
    string LogoUrl,
    bool IsRepresentative);
