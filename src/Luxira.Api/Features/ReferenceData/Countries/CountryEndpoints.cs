using Microsoft.AspNetCore.Http.HttpResults;

namespace Luxira.Api.Features.ReferenceData.Countries;

internal static class CountryEndpoints
{
    internal static IEndpointRouteBuilder MapCountryEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
                "/api/v1/reference-data/countries",
                GetCountries)
            .WithName("ReferenceData_GetCountries")
            .WithTags("Reference Data")
            .WithSummary("List the countries supported by Luxira")
            .Produces<CountryResponse[]>();

        endpoints.MapGet(
                "/DataList/GetAllCountries",
                GetCountries)
            .WithName("LegacyDataList_GetAllCountries")
            .WithTags("Legacy Compatibility")
            .WithSummary("List countries using the legacy DataList route")
            .WithDescription(
                "Compatibility route for existing Luxira clients. Prefer /api/v1/reference-data/countries for new clients.")
            .Produces<CountryResponse[]>();

        endpoints.MapGet(
                "/api/v1/reference-data/countries/preparation-for-delivery",
                GetPreparationForDeliveryCountries)
            .WithName("ReferenceData_GetPreparationForDeliveryCountries")
            .WithTags("Reference Data")
            .WithSummary("List countries supported by preparation for delivery")
            .Produces<CountryResponse[]>();

        endpoints.MapGet(
                "/DataList/GetPfdCountries",
                GetPreparationForDeliveryCountries)
            .WithName("LegacyDataList_GetPfdCountries")
            .WithTags("Legacy Compatibility")
            .WithSummary("List preparation-for-delivery countries using the legacy route")
            .WithDescription(
                "Compatibility route for the existing Prepare For Delivery page.")
            .Produces<CountryResponse[]>();

        return endpoints;
    }

    private static Ok<CountryResponse[]> GetCountries() =>
        TypedResults.Ok(CountryCatalog.All);

    private static Ok<CountryResponse[]> GetPreparationForDeliveryCountries() =>
        TypedResults.Ok(CountryCatalog.PreparationForDelivery);
}
