using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Luxira.Api.IntegrationTests;

public sealed class LuxiraUnavailableInfrastructureFactory
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = LuxiraApiFactory.JwtIssuer,
                    ["Jwt:Audience"] = LuxiraApiFactory.JwtAudience,
                    ["Jwt:Key"] = LuxiraApiFactory.JwtKey,
                }));
    }
}
