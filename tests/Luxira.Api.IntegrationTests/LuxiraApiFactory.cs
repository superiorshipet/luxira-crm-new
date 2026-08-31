using Luxira.Application.Features.DeliveryCompanies.GetDeliveryPrice;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryCompanies;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryRepresentatives;
using Luxira.Application.Features.SearchKeywords.ListSearchKeywords;
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
            services.Replace(ServiceDescriptor.Singleton<
                IListDeliveryCompaniesRepository,
                FakeListDeliveryCompaniesRepository>());
            services.Replace(ServiceDescriptor.Singleton<
                IListDeliveryRepresentativesRepository,
                FakeListDeliveryRepresentativesRepository>());
            services.Replace(ServiceDescriptor.Singleton<
                IGetDeliveryPriceRepository,
                FakeGetDeliveryPriceRepository>());
            services.Replace(ServiceDescriptor.Singleton<
                IListDeliveryOptionsRepository,
                FakeListDeliveryOptionsRepository>());
            services.Replace(ServiceDescriptor.Singleton<
                IListSearchKeywordsRepository,
                FakeListSearchKeywordsRepository>());
        });
    }

    private sealed class FakeListDeliveryCompaniesRepository
        : IListDeliveryCompaniesRepository
    {
        public Task<IReadOnlyList<DeliveryCompanyRecord>> ListAsync(
            IReadOnlyCollection<int>? countryIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = DeliveryTestData.Companies
                .Where(company => countryIds is not { Count: > 0 } ||
                    countryIds.Contains(company.CountryId))
                .Select(company => new DeliveryCompanyRecord(
                    company.Id,
                    company.Name,
                    company.LogoUrl))
                .ToArray();
            return Task.FromResult<IReadOnlyList<DeliveryCompanyRecord>>(result);
        }
    }

    private sealed class FakeListDeliveryRepresentativesRepository
        : IListDeliveryRepresentativesRepository
    {
        public Task<IReadOnlyList<DeliveryRepresentativeRecord>> ListAsync(
            IReadOnlyCollection<int>? countryIds,
            IReadOnlyCollection<string>? cityIds,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = DeliveryTestData.Representatives
                .Where(representative => countryIds is not { Count: > 0 } ||
                    countryIds.Contains(representative.CountryId))
                .Where(representative =>
                    cityIds is null ||
                    !cityIds.Any(city => !string.IsNullOrWhiteSpace(city)) ||
                    cityIds.Contains(representative.City))
                .Select(representative => new DeliveryRepresentativeRecord(
                    representative.Id,
                    representative.Name,
                    representative.LogoUrl))
                .ToArray();
            return Task.FromResult<IReadOnlyList<DeliveryRepresentativeRecord>>(result);
        }
    }

    private sealed class FakeGetDeliveryPriceRepository : IGetDeliveryPriceRepository
    {
        public Task<decimal> GetAsync(
            int deliveryCompanyId,
            int countryId,
            string? cityId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = DeliveryTestData.Prices
                .Where(price =>
                    price.DeliveryCompanyId == deliveryCompanyId &&
                    price.CountryId == countryId &&
                    (price.City is null || price.City == cityId || cityId is null))
                .OrderByDescending(price => price.City == cityId)
                .Select(price => (decimal?)price.Amount)
                .FirstOrDefault() ?? 0m;
            return Task.FromResult(result);
        }
    }

    private sealed class FakeListDeliveryOptionsRepository
        : IListDeliveryOptionsRepository
    {
        public Task<int?> GetAssignedCompanyIdForOrderAsync(
            int orderId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<int?>(orderId == 500 ? 1 : null);
        }

        public Task<IReadOnlyList<DeliveryOptionRecord>> ListAsync(
            int? countryId,
            string? cityId,
            int? restrictToCompanyId,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = DeliveryTestData.Companies
                .Select(company => new DeliveryTestData.Option(
                    company.Id,
                    company.Name,
                    company.LogoUrl,
                    company.CountryId,
                    null,
                    false))
                .Concat(DeliveryTestData.Representatives.Select(representative =>
                    new DeliveryTestData.Option(
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
                .Select(option => new DeliveryOptionRecord(
                    option.Id,
                    option.Name,
                    option.LogoUrl,
                    option.IsRepresentative))
                .ToArray();
            return Task.FromResult<IReadOnlyList<DeliveryOptionRecord>>(options);
        }
    }

    private static class DeliveryTestData
    {
        internal static readonly Company[] Companies =
        [
            new(1, "Iraq Express", "logos/iraq-express.svg", 1),
            new(2, "UAE Express", null, 2),
        ];

        internal static readonly Representative[] Representatives =
        [
            new(101, "Baghdad Representative", "/logos/baghdad.svg", 1, "بغداد"),
            new(102, "Basra Representative", null, 1, "البصرة"),
            new(103, "Dubai Representative", "https://cdn.example.test/dubai.svg", 2, "دبي"),
        ];

        internal static readonly Price[] Prices =
        [
            new(1, 1, null, 10m),
            new(1, 1, "بغداد", 15.5m),
        ];

        internal sealed record Company(
            int Id,
            string Name,
            string? LogoUrl,
            int CountryId);

        internal sealed record Representative(
            int Id,
            string Name,
            string? LogoUrl,
            int CountryId,
            string City);

        internal sealed record Price(
            int DeliveryCompanyId,
            int CountryId,
            string? City,
            decimal Amount);

        internal sealed record Option(
            int Id,
            string Name,
            string? LogoUrl,
            int CountryId,
            string? City,
            bool IsRepresentative);
    }

    private sealed class FakeListSearchKeywordsRepository
        : IListSearchKeywordsRepository
    {
        private static readonly SearchKeywordRecord[] Keywords =
        [
            new(
                2,
                "طلبات بغداد",
                "طلبات بغداد",
                "City",
                "بغداد",
                "بغداد",
                "دول ومناطق",
                true,
                new DateTime(2026, 1, 2),
                "Admin",
                null,
                null,
                false),
            new(
                1,
                "طلبات قديمة",
                "طلبات قديمه",
                "DateScope",
                "Old",
                null,
                "فترات زمنية",
                false,
                new DateTime(2026, 1, 1),
                "Admin",
                null,
                null,
                false),
        ];

        public Task<IReadOnlyList<SearchKeywordRecord>> ListAsync(
            SearchKeywordFilter filter,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var result = Keywords
                .Where(keyword => filter.Search is null ||
                    Contains(keyword.Phrase, filter.Search) ||
                    Contains(keyword.DisplayLabel, filter.Search) ||
                    Contains(keyword.Category, filter.Search) ||
                    Contains(keyword.TargetValue, filter.Search))
                .Where(keyword => filter.TargetType is null ||
                    keyword.TargetType == filter.TargetType)
                .Where(keyword => filter.Category is null ||
                    keyword.Category == filter.Category)
                .Where(keyword => !filter.IsActive.HasValue ||
                    keyword.IsActive == filter.IsActive.Value)
                .OrderByDescending(keyword => keyword.IsActive)
                .ThenByDescending(keyword => keyword.Id)
                .ToArray();
            return Task.FromResult<IReadOnlyList<SearchKeywordRecord>>(result);
        }

        private static bool Contains(string? value, string search) =>
            value?.Contains(search, StringComparison.Ordinal) == true;
    }
}
