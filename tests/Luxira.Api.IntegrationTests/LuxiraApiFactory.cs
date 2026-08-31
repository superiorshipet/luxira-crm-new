using Luxira.Infrastructure.DeliveryCompanies;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Luxira.Api.IntegrationTests;

public sealed class LuxiraApiFactory : WebApplicationFactory<Program>
{
    internal const string JwtIssuer = "Luxira.IntegrationTests";
    internal const string JwtAudience = "Luxira.IntegrationTests.Clients";
    internal const string JwtKey =
        "integration-tests-only-signing-key-00000000000000000000";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = JwtIssuer,
                    ["Jwt:Audience"] = JwtAudience,
                    ["Jwt:Key"] = JwtKey,
                }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDeliveryCompanyReader>();
            services.AddSingleton<IDeliveryCompanyReader, FakeDeliveryCompanyReader>();
            services.RemoveAll<IDeliveryPriceReader>();
            services.AddSingleton<IDeliveryPriceReader, FakeDeliveryPriceReader>();
        });
    }

    private sealed class FakeDeliveryCompanyReader : IDeliveryCompanyReader
    {
        private static readonly Company[] Companies =
        [
            new(1, "Iraq Express", "logos/iraq-express.svg", 1),
            new(2, "UAE Express", null, 2),
        ];

        public Task<IReadOnlyList<DeliveryCompanyListItem>> ListCompaniesAsync(
            IReadOnlyCollection<int>? countryIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = Companies
                .Where(company =>
                    countryIds is not { Count: > 0 } ||
                    countryIds.Contains(company.CountryId))
                .Select(company => new DeliveryCompanyListItem(
                    company.Id,
                    company.Name,
                    company.LogoUrl))
                .ToArray();
            return Task.FromResult<IReadOnlyList<DeliveryCompanyListItem>>(result);
        }

        public Task<IReadOnlyList<DeliveryCompanyListItem>> ListRepresentativesAsync(
            IReadOnlyCollection<int>? countryIds,
            IReadOnlyCollection<string>? cityIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = Representatives
                .Where(representative =>
                    countryIds is not { Count: > 0 } ||
                    countryIds.Contains(representative.CountryId))
                .Where(representative =>
                    cityIds is null ||
                    !cityIds.Any(city => !string.IsNullOrWhiteSpace(city)) ||
                    cityIds.Contains(representative.City))
                .Select(representative => new DeliveryCompanyListItem(
                    representative.Id,
                    representative.Name,
                    representative.LogoUrl))
                .ToArray();
            return Task.FromResult<IReadOnlyList<DeliveryCompanyListItem>>(result);
        }

        public Task<int?> GetAssignedCompanyIdForOrderAsync(
            int orderId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<int?>(orderId == 500 ? 1 : null);
        }

        public Task<IReadOnlyList<DeliveryOptionListItem>> ListCompaniesAndRepresentativesAsync(
            int? countryId,
            string? cityId,
            int? restrictToCompanyId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = Companies
                .Select(company => new Option(
                    company.Id,
                    company.Name,
                    company.LogoUrl,
                    company.CountryId,
                    null,
                    false))
                .Concat(Representatives.Select(representative => new Option(
                    representative.Id,
                    representative.Name,
                    representative.LogoUrl,
                    representative.CountryId,
                    representative.City,
                    true)))
                .Where(option => !restrictToCompanyId.HasValue ||
                    option.Id == restrictToCompanyId.Value)
                .Where(option => !countryId.HasValue ||
                    option.CountryId == countryId.Value)
                .Where(option => !option.IsRepresentative ||
                    string.IsNullOrEmpty(cityId) || option.City == cityId)
                .Select(option => new DeliveryOptionListItem(
                    option.Id,
                    option.Name,
                    option.LogoUrl,
                    option.IsRepresentative))
                .ToArray();
            return Task.FromResult<IReadOnlyList<DeliveryOptionListItem>>(options);
        }

        private static readonly Representative[] Representatives =
        [
            new(101, "Baghdad Representative", "/logos/baghdad.svg", 1, "بغداد"),
            new(102, "Basra Representative", null, 1, "البصرة"),
            new(103, "Dubai Representative", "https://cdn.example.test/dubai.svg", 2, "دبي"),
        ];

        private sealed record Company(
            int Id,
            string Name,
            string? LogoUrl,
            int CountryId);

        private sealed record Representative(
            int Id,
            string Name,
            string? LogoUrl,
            int CountryId,
            string City);

        private sealed record Option(
            int Id,
            string Name,
            string? LogoUrl,
            int CountryId,
            string? City,
            bool IsRepresentative);
    }

    private sealed class FakeDeliveryPriceReader : IDeliveryPriceReader
    {
        public Task<decimal> GetPriceAsync(
            int deliveryCompanyId,
            int countryId,
            string? cityId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var prices = new[]
            {
                new Price(1, 1, null, 10m),
                new Price(1, 1, "بغداد", 15.5m),
            };
            var result = prices
                .Where(price =>
                    price.DeliveryCompanyId == deliveryCompanyId &&
                    price.CountryId == countryId &&
                    (price.City is null || price.City == cityId || cityId is null))
                .OrderByDescending(price => price.City == cityId)
                .Select(price => (decimal?)price.Amount)
                .FirstOrDefault() ?? 0m;
            return Task.FromResult(result);
        }

        private sealed record Price(
            int DeliveryCompanyId,
            int CountryId,
            string? City,
            decimal Amount);
    }
}
