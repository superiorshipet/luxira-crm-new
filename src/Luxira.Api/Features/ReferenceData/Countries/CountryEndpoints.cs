using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.ReferenceData.Countries;

internal static class CountryEndpoints
{
    internal static IEndpointRouteBuilder MapCountryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var publicEndpoints = endpoints
            .MapGroup(string.Empty)
            .AllowAnonymous();

        publicEndpoints.MapGet(
                "/api/v1/reference-data/countries",
                GetCountries)
            .WithName("ReferenceData_GetCountries")
            .WithTags("Reference Data")
            .WithSummary("List the countries supported by Luxira")
            .CacheOutput("ReferenceData")
            .Produces<CountryResponse[]>();

        publicEndpoints.MapGet(
                "/DataList/GetAllCountries",
                GetCountries)
            .WithName("LegacyDataList_GetAllCountries")
            .WithTags("Legacy Compatibility")
            .WithSummary("List countries using the legacy DataList route")
            .WithDescription(
                "Compatibility route for existing Luxira clients. Prefer /api/v1/reference-data/countries for new clients.")
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

        publicEndpoints.MapGet(
                "/DataList/GetPfdCountries",
                GetPreparationForDeliveryCountries)
            .WithName("LegacyDataList_GetPfdCountries")
            .WithTags("Legacy Compatibility")
            .WithSummary("List preparation-for-delivery countries using the legacy route")
            .WithDescription(
                "Compatibility route for the existing Prepare For Delivery page.")
            .CacheOutput("ReferenceData")
            .Produces<CountryResponse[]>();

        publicEndpoints.MapGet(
                "/api/v1/reference-data/cities",
                GetCities)
            .WithName("ReferenceData_GetCitiesByCountry")
            .WithTags("Reference Data")
            .WithSummary("List distinct cities for the selected countries")
            .Produces<string[]>();

        publicEndpoints.MapGet(
                "/DataList/GetCitiesByCountry",
                GetCities)
            .WithName("LegacyDataList_GetCitiesByCountry")
            .WithTags("Legacy Compatibility")
            .WithSummary("List cities using the legacy DataList route")
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
