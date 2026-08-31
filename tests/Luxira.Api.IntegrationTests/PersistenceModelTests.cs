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

        var price = context.Model.GetEntityTypes()
            .Single(candidate =>
                candidate.GetTableName() == "DeliveryCompanyPrices");
        Assert.Equal("decimal(18,2)", price.FindProperty("Price")!.GetColumnType());
        Assert.NotNull(price.FindProperty("DeliveryCompanyId"));

        Assert.Contains(context.Model.GetEntityTypes(), candidate =>
            candidate.GetTableName() == "Orders" &&
            candidate.FindProperty("ManufacturingCompanyId") is not null);
        Assert.Contains(context.Model.GetEntityTypes(), candidate =>
            candidate.GetTableName() == "StoreDeliveryCompanyAssignments" &&
            candidate.FindProperty("IsManualTransfer") is not null);

        var searchKeyword = context.Model.GetEntityTypes()
            .Single(candidate => candidate.GetTableName() == "HomeSearchKeywords");
        Assert.Equal(250, searchKeyword.FindProperty("Phrase")!.GetMaxLength());
        Assert.Equal(50, searchKeyword.FindProperty("TargetType")!.GetMaxLength());
        Assert.NotNull(searchKeyword.FindProperty("IsSingleResult"));
    }
}
