using System.Text.Json;
using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Controllers;
using Luxira.Api.Features.ManufacturingCompanies.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Luxira.Tests;

public sealed class TraineeStoresParityTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Create_uses_trainee_tables_and_returns_the_legacy_card_contract()
    {
        await using var context = CreateContext();
        context.ManufacturingCompanies.AddRange(
            new ManufacturingCompany { Id = 1, Name = "Store B", ImageUrl = "images/b.png" },
            new ManufacturingCompany { Id = 2, Name = "Store A", ImageUrl = null });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.Create(new TraineeStoreSaveRequest
        {
            Name = "  Trainee  ",
            PhoneNumber = " 0100 ",
            StoreIds = [2, 1, 1, 999]
        });

        var json = Json(result);
        Assert.True(json.GetProperty("success").GetBoolean());
        Assert.Equal("Trainee", json.GetProperty("card").GetProperty("name").GetString());
        Assert.Equal("0100", json.GetProperty("card").GetProperty("phoneNumber").GetString());
        Assert.Equal<string>(["Store A", "Store B"], json.GetProperty("card").GetProperty("stores")
            .EnumerateArray().Select(item => item.GetProperty("name").GetString()!).ToArray());
        Assert.Single(await context.TraineeStores.ToListAsync());
        Assert.Equal(2, await context.TraineeStoreManufacturingCompanies.CountAsync());
        Assert.Empty(await context.StoreCodeFolders.ToListAsync());
    }

    [Fact]
    public async Task Update_replaces_store_links_and_index_returns_filter_options()
    {
        await using var context = CreateContext();
        context.ManufacturingCompanies.AddRange(
            new ManufacturingCompany { Id = 1, Name = "One" },
            new ManufacturingCompany { Id = 2, Name = "Two" });
        var trainee = new TraineeStore
        {
            Id = 10,
            Name = "Before",
            PhoneNumber = "111",
            ManufacturingCompanies =
            [
                new TraineeStoreManufacturingCompany { ManufacturingCompanyId = 1 }
            ]
        };
        context.TraineeStores.Add(trainee);
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var update = await controller.Update(new TraineeStoreSaveRequest
        {
            Id = 10,
            Name = "After",
            PhoneNumber = "222",
            StoreIds = [2]
        });
        var index = await controller.Index();

        Assert.True(Json(update).GetProperty("success").GetBoolean());
        Assert.Equal([2], await context.TraineeStoreManufacturingCompanies
            .Where(item => item.TraineeStoreId == 10)
            .Select(item => item.ManufacturingCompanyId)
            .ToListAsync());
        var indexJson = Json(index);
        Assert.Equal("After", indexJson.GetProperty("cards")[0].GetProperty("name").GetString());
        Assert.Equal("222", indexJson.GetProperty("phoneOptions")[0].GetString());
        Assert.Equal(2, indexJson.GetProperty("storeOptions").GetArrayLength());
    }

    [Fact]
    public void Legacy_form_routes_are_preserved()
    {
        Assert.Contains("/TraineeStores/Create", Routes(nameof(TraineeStoresController.CreateLegacy)));
        Assert.Contains("/TraineeStores/Update", Routes(nameof(TraineeStoresController.UpdateLegacy)));
        Assert.Contains("/TraineeStores/Delete", Routes(nameof(TraineeStoresController.Delete)));
    }

    private static IEnumerable<string?> Routes(string methodName) =>
        typeof(TraineeStoresController).GetMethod(methodName)!
            .GetCustomAttributes(typeof(HttpMethodAttribute), true)
            .Cast<HttpMethodAttribute>()
            .Select(attribute => attribute.Template);

    private static JsonElement Json(IActionResult result)
    {
        var value = Assert.IsType<OkObjectResult>(result).Value;
        return JsonSerializer.SerializeToElement(value, JsonOptions);
    }

    private static TraineeStoresController CreateController(ApplicationDbContext context) =>
        new(context, NullLogger<TraineeStoresController>.Instance);

    private static ApplicationDbContext CreateContext() => new(
        new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"trainee-stores-{Guid.NewGuid():N}")
            .Options);
}
