using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Tests;

public class ManufacturingSchemaTests
{
    [Fact]
    public void MainProduct_Mapping_UsesLegacyColumnsAndStoreRelationship()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(MainProduct));

        Assert.NotNull(entity);
        Assert.Equal("MainProducts", entity.GetTableName());
        Assert.NotNull(entity.FindProperty(nameof(MainProduct.Price)));
        Assert.NotNull(entity.FindProperty(nameof(MainProduct.IsDeleted)));
        Assert.NotNull(entity.FindNavigation(nameof(MainProduct.ManufacturingCompany)));
    }

    [Fact]
    public void ProductImage_Mapping_UsesStoreCatalogueSchema()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(ProductImage));

        Assert.NotNull(entity);
        Assert.Equal("ProductImages", entity.GetTableName());
        Assert.NotNull(entity.FindProperty(nameof(ProductImage.ProductName)));
        Assert.NotNull(entity.FindProperty(nameof(ProductImage.ManufacturingCompanyId)));
        Assert.Null(entity.FindProperty("MainProductId"));
        Assert.NotNull(entity.FindNavigation(nameof(ProductImage.ManufacturingCompany)));
    }

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"schema-tests-{Guid.NewGuid():N}")
            .Options);
}
