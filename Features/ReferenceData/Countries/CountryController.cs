using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.ReferenceData.Countries;

internal static class CountryController
{
    internal static IEndpointRouteBuilder MapCountryController(
        this IEndpointRouteBuilder endpoints)
    {
        var publicEndpoints = endpoints.MapGroup(string.Empty).AllowAnonymous();

        publicEndpoints.MapGet("/api/v1/reference-data/countries", GetCountries)
            .WithName("ReferenceData_GetCountries")
            .WithTags("Reference Data")
            .WithSummary("List the countries supported by Luxira")
            .CacheOutput("ReferenceData")
            .Produces<CountryResponse[]>();

        publicEndpoints.MapGet(
                "/api/v1/reference-data/countries/preparation-for-delivery",
                GetPreparationForDeliveryCountries)
            .WithName("ReferenceData_GetPreparationForDeliveryCountries")
            .WithTags("Reference Data")
            .WithSummary("List countries supported by preparation for delivery")
            .CacheOutput("ReferenceData")
            .Produces<CountryResponse[]>();

        publicEndpoints.MapGet("/api/v1/reference-data/cities", GetCities)
            .WithName("ReferenceData_GetCitiesByCountry")
            .WithTags("Reference Data")
            .WithSummary("List distinct cities for the selected countries")
            .Produces<string[]>();

        return endpoints;
    }

    private static Ok<CountryResponse[]> GetCountries() =>
        TypedResults.Ok(CountryCatalog.All);

    private static Ok<CountryResponse[]> GetPreparationForDeliveryCountries() =>
        TypedResults.Ok(CountryCatalog.PreparationForDelivery);

    private static Ok<string[]> GetCities(
        [FromQuery(Name = "countryIds")] int[]? countryIds) =>
        TypedResults.Ok(CountryCityCatalog.GetDistinctCities(countryIds));
}
