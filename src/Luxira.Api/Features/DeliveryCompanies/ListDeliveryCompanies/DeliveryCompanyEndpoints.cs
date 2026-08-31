using Luxira.Infrastructure.DeliveryCompanies;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.DeliveryCompanies.ListDeliveryCompanies;

internal static class DeliveryCompanyEndpoints
{
    internal static IEndpointRouteBuilder MapDeliveryCompanyEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/delivery-companies",
                ListDeliveryCompanies)
            .WithName("DeliveryCompanies_List")
            .WithTags("Delivery Companies")
            .WithSummary("List visible delivery companies")
            .Produces<DeliveryCompanyResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        endpoints.MapGet(
                "/DataList/GetAllDeliveryCompanies",
                ListDeliveryCompanies)
            .WithName("LegacyDataList_GetAllDeliveryCompanies")
            .WithTags("Legacy Compatibility")
            .WithSummary("List visible delivery companies using the legacy route")
            .Produces<DeliveryCompanyResponse[]>()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status503ServiceUnavailable);

        return endpoints;
    }

    private static async Task<Results<
        Ok<DeliveryCompanyResponse[]>,
        ProblemHttpResult>> ListDeliveryCompanies(
        [FromQuery(Name = "countryIds")] int[]? countryIds,
        IDeliveryCompanyReader reader,
        CancellationToken cancellationToken)
    {
        try
        {
            var companies = await reader.ListCompaniesAsync(
                countryIds,
                cancellationToken);
            return TypedResults.Ok(
                companies
                    .Select(company => new DeliveryCompanyResponse(
                        company.Id,
                        company.Name,
                        NormalizeMediaUrl(company.LogoUrl)))
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

    private static string NormalizeMediaUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "/static/DefaultImage.svg";
        }

        return url.StartsWith('/') ||
            url.StartsWith("http://", StringComparison.Ordinal) ||
            url.StartsWith("https://", StringComparison.Ordinal)
                ? url
                : "/" + url;
    }
}

internal sealed record DeliveryCompanyResponse(
    int Id,
    string Name,
    string LogoUrl);
