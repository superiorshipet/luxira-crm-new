using Luxira.Api.Features.Warehouses.Controllers;
using Luxira.Api.Features.ManufacturingCompanies.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Luxira.Tests;

public sealed class WarehouseLegacyContractTests
{
    [Theory]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.Index), "GET", "/Warehouse/Index")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.Index), "POST", "/Warehouse/Index")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.Create), "GET", "/Warehouse/Create")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.Edit), "GET", "/Warehouse/Edit")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.Edit), "POST", "/Warehouse/Edit")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.Details), "GET", "/Warehouse/Details")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.Details), "POST", "/Warehouse/Details")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.SetIsShown), "POST", "/Warehouse/SetIsShown")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.GetSubWarehouses), "GET", "/Warehouse/GetSubWarehouses")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.GetSubWarehouses), "POST", "/Warehouse/GetSubWarehouses")]
    [InlineData(typeof(WarehouseController), nameof(WarehouseController.AllWarehousesPdf), "POST", "/Warehouse/AllWarehousesPdf")]
    [InlineData(typeof(MainWareHouseController), nameof(MainWareHouseController.Index), "GET", "Index")]
    [InlineData(typeof(MainWareHouseController), nameof(MainWareHouseController.Create), "GET", "Create")]
    [InlineData(typeof(MainWareHouseController), nameof(MainWareHouseController.Create), "POST", "Create")]
    [InlineData(typeof(MainWareHouseController), nameof(MainWareHouseController.Edit), "GET", "Edit")]
    [InlineData(typeof(MainWareHouseController), nameof(MainWareHouseController.Edit), "POST", "Edit")]
    [InlineData(typeof(SubWarehouseController), nameof(SubWarehouseController.Index), "GET", "Index")]
    [InlineData(typeof(SubWarehouseController), nameof(SubWarehouseController.Create), "GET", "Create")]
    [InlineData(typeof(SubWarehouseController), nameof(SubWarehouseController.Create), "POST", "Create")]
    [InlineData(typeof(SubWarehouseController), nameof(SubWarehouseController.Edit), "GET", "Edit")]
    [InlineData(typeof(SubWarehouseController), nameof(SubWarehouseController.Edit), "POST", "Edit")]
    public void RouteIsPublished(Type controller, string method, string verb, string template)
    {
        var attributes = controller.GetMethods().Where(info => info.Name == method)
            .SelectMany(info => info.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>());
        Assert.Contains(attributes, attribute => attribute.HttpMethods.Contains(verb) && attribute.Template == template);
    }

    [Theory]
    [InlineData(nameof(ManufacturingCompanyController.GetCompanies), "GET", "/ManufacturingCompany/Index")]
    [InlineData(nameof(ManufacturingCompanyController.GetCompanies), "POST", "/ManufacturingCompany/Index")]
    [InlineData(nameof(ManufacturingCompanyController.Create), "GET", "/ManufacturingCompany/Create")]
    [InlineData(nameof(ManufacturingCompanyController.Create), "POST", "/ManufacturingCompany/Create")]
    [InlineData(nameof(ManufacturingCompanyController.Edit), "GET", "/ManufacturingCompany/Edit")]
    [InlineData(nameof(ManufacturingCompanyController.Edit), "POST", "/ManufacturingCompany/Edit")]
    [InlineData(nameof(ManufacturingCompanyController.SetIsShown), "POST", "/ManufacturingCompany/SetIsShown")]
    public void ManufacturingCompanyRouteIsPublished(string method, string verb, string template)
    {
        var attributes = typeof(ManufacturingCompanyController).GetMethods().Where(info => info.Name == method)
            .SelectMany(info => info.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>());
        Assert.Contains(attributes, attribute => attribute.HttpMethods.Contains(verb) && attribute.Template == template);
    }
}
