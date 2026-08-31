using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Luxira.Api.IntegrationTests;

public sealed class ReferenceDataContractTests(LuxiraApiFactory factory)
    : IClassFixture<LuxiraApiFactory>
{
    private readonly HttpClient _client = factory.CreateClient(
        new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
        });

    [Fact]
    public async Task CountriesPreserveLegacyContractAndRouteParity()
    {
        using var canonicalResponse = await _client.GetAsync(
            "/api/v1/reference-data/countries");
        using var legacyResponse = await _client.GetAsync(
            "/DataList/GetAllCountries");

        Assert.Equal(HttpStatusCode.OK, canonicalResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, legacyResponse.StatusCode);

        var canonicalJson = await canonicalResponse.Content.ReadAsStringAsync();
        var legacyJson = await legacyResponse.Content.ReadAsStringAsync();
        var countries = await canonicalResponse.Content
            .ReadFromJsonAsync<CountryContract[]>();

        Assert.Equal(canonicalJson, legacyJson);
        Assert.NotNull(countries);
        Assert.Equal(16, countries.Length);
        Assert.Equal(
            new CountryContract(1, "العراق", "/Countries/iraq.svg"),
            countries[0]);
        Assert.Equal(
            new CountryContract(16, "مصر", "/Countries/egypt.svg"),
            countries[^1]);
    }

    [Fact]
    public async Task PreparationCountriesPreserveLegacyOrderAndRouteParity()
    {
        var canonicalJson = await _client.GetStringAsync(
            "/api/v1/reference-data/countries/preparation-for-delivery");
        var legacyJson = await _client.GetStringAsync(
            "/DataList/GetPfdCountries");
        var countries = await _client.GetFromJsonAsync<CountryContract[]>(
            "/api/v1/reference-data/countries/preparation-for-delivery");

        Assert.Equal(canonicalJson, legacyJson);
        Assert.NotNull(countries);
        Assert.Equal([1, 4, 5, 2], countries.Select(country => country.Id));
    }

    [Fact]
    public async Task FailureReasonsPreserveDisplayNamesAndRouteParity()
    {
        var canonicalJson = await _client.GetStringAsync(
            "/api/v1/reference-data/failure-reasons");
        var legacyJson = await _client.GetStringAsync(
            "/DataList/GetAllFailureReasons");
        var reasons = await _client.GetFromJsonAsync<FailureReasonContract[]>(
            "/api/v1/reference-data/failure-reasons");

        Assert.Equal(canonicalJson, legacyJson);
        Assert.NotNull(reasons);
        Assert.Equal(11, reasons.Length);
        Assert.Equal(new FailureReasonContract(9, "تأجيل الاستلام"), reasons[8]);
        Assert.Equal(
            new FailureReasonContract(11, "الطلب غير مطابق للمطلوب"),
            reasons[^1]);
    }

    [Fact]
    public async Task CitiesPreserveLegacyDistinctOrderAndRouteParity()
    {
        const string query = "?countryIds=5&countryIds=1";
        var canonicalJson = await _client.GetStringAsync(
            "/api/v1/reference-data/cities" + query);
        var legacyJson = await _client.GetStringAsync(
            "/DataList/GetCitiesByCountry" + query);
        var cities = await _client.GetFromJsonAsync<string[]>(
            "/api/v1/reference-data/cities" + query);
        var noSelection = await _client.GetFromJsonAsync<string[]>(
            "/DataList/GetCitiesByCountry");
        var reversedCountries = await _client.GetFromJsonAsync<string[]>(
            "/api/v1/reference-data/cities?countryIds=1&countryIds=5");

        Assert.Equal(canonicalJson, legacyJson);
        Assert.NotNull(cities);
        Assert.Equal("مسقط", cities[0]);
        Assert.Equal("بعقوبة", cities[^1]);
        Assert.Equal(cities.Length, cities.Distinct(StringComparer.Ordinal).Count());
        Assert.Empty(noSelection ?? []);
        Assert.NotNull(reversedCountries);
        Assert.Equal("بغداد", reversedCountries[0]);
        Assert.Equal("الوسطى", reversedCountries[^1]);
    }

    private sealed record CountryContract(
        int Id,
        string Name,
        string ImageUrl);

    private sealed record FailureReasonContract(
        int Id,
        string Name);
}
