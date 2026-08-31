using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.IntegrationTests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void DeliveryCompanyReadModelMatchesLegacyTableWithoutConnecting()
    {
        var options = new DbContextOptionsBuilder<LuxiraReadDbContext>()
            .UseSqlServer(
                "Server=127.0.0.1,1;Database=NeverConnect;User Id=none;Password=none;TrustServerCertificate=True")
            .Options;
        using var context = new LuxiraReadDbContext(options);

        var entity = context.Model.GetEntityTypes()
            .Single(candidate => candidate.GetTableName() == "DeliveryCompanies");

        Assert.Equal("Id", entity.FindPrimaryKey()!.Properties.Single().Name);
        Assert.Equal(100, entity.FindProperty("Name")!.GetMaxLength());
        Assert.NotNull(entity.FindProperty("Country"));
        Assert.NotNull(entity.FindProperty("IsShown"));
        Assert.NotNull(entity.FindProperty("IsRepresentative"));
    }
}
