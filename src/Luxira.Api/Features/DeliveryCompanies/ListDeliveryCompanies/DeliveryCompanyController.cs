using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryCompanies;
using Luxira.Api.Errors;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.ListDeliveryCompanies;

internal static class DeliveryCompanyController
{
    internal static IEndpointRouteBuilder MapDeliveryCompanyController(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/delivery-companies",
                ListDeliveryCompanies)
            .WithName("DeliveryCompanies_List")
            .WithTags("Delivery Companies")
            .WithSummary("List visible delivery companies")
            .Produces<DeliveryCompanyResult[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/DataList/GetAllDeliveryCompanies",
                ListDeliveryCompanies)
            .WithName("LegacyDataList_GetAllDeliveryCompanies")
            .WithTags("Legacy Compatibility")
            .WithSummary("List visible delivery companies using the legacy route")
            .Produces<DeliveryCompanyResult[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<
        Ok<IReadOnlyList<DeliveryCompanyResult>>,
        ProblemHttpResult>> ListDeliveryCompanies(
        [FromQuery(Name = "countryIds")] int[]? countryIds,
        ListDeliveryCompaniesService service,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await service.ExecuteAsync(countryIds, cancellationToken);
            return TypedResults.Ok(result);
        }
        catch (ReadStoreUnavailableException exception)
        {
            return ReadStoreProblem.Create(exception);
        }
    }
}
