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

        private sealed record Company(
            int Id,
            string Name,
            string? LogoUrl,
            int CountryId);
    }
}
