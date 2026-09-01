using Luxira.Api.Data;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.DeliveryCompanies.Models;
using Luxira.Api.Features.SearchKeywords.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Luxira.Api.IntegrationTests;

public sealed class LuxiraApiFactory : WebApplicationFactory<Program>
{
    internal const string JwtIssuer = "Luxira.IntegrationTests";
    internal const string JwtAudience = "Luxira.IntegrationTests.Clients";
    internal const string JwtKey = "integration-tests-only-signing-key-00000000000000000000";
    private readonly string _dbName = $"LuxiraTestDb_{Guid.NewGuid():N}";

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
                    ["ConnectionStrings:DefaultConnection"] = $"InMemory:{_dbName}"
                }));

        builder.ConfigureServices(services =>
        {
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            try
            {
                if (db.Users.Find("test-user-1") == null)
                {
                    db.Users.Add(new ApplicationUser
                    {
                        Id = "test-user-1",
                        UserName = "testadmin",
                        NormalizedUserName = "TESTADMIN",
                        Email = "admin@luxiracrm.com",
                        Name = "Test Admin",
                        Role = "Admin",
                        Country = 1,
                        AcessId = 1,
                        IsActive = true
                    });
                }

                if (db.DeliveryCompanies.Find(1) == null)
                {
                    var dc = new DeliveryCompany
                    {
                        Id = 1,
                        Name = "شركة النصر للشحن",
                        DisplayName = "النصر",
                        Country = 1,
                        Address = "بغداد - الكرادة",
                        PhoneNumber = "07700000000",
                        IdNumber = "123456",
                        UserId = "test-user-1",
                        IsActive = true,
                        IsShown = true,
                        IsRepresentative = false
                    };
                    dc.Prices.Add(new DeliveryCompanyPrice { Id = 1, Country = 1, Price = 5000m, DeliveryCompanyId = 1 });
                    db.DeliveryCompanies.Add(dc);
                }

                if (db.SearchKeywordOptions.Find(1) == null)
                {
                    db.SearchKeywordOptions.Add(new SearchKeywordOption
                    {
                        Id = 1,
                        Keyword = "عطور",
                        TargetType = "Product",
                        Category = "General",
                        IsActive = true,
                        SortOrder = 1
                    });
                }

                db.SaveChanges();
            }
            catch
            {
                // In-memory store already seeded
            }
        });
    }
}
