using Luxira.Api.Features.ReferenceData;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Luxira.Tests;

public sealed class DataListLegacyContractTests
{
    [Theory]
    [InlineData("GetAllEmployees", "GET")]
    [InlineData("GetAllEmployees", "POST")]
    [InlineData("GetAllEmployeesintId", "GET")]
    [InlineData("GetAllEmployeesintId", "POST")]
    [InlineData("GetAllStores", "GET")]
    [InlineData("GetAllDeliveryCompanies", "POST")]
    [InlineData("GetAllDeliveryRepresentatives", "GET")]
    [InlineData("GetAllDeliveryRepresentatives", "POST")]
    [InlineData("GetAllDeliveryCompaniesAndRepresentatives", "GET")]
    [InlineData("GetAllDeliveryCompaniesAndRepresentatives", "POST")]
    [InlineData("GetDeliveryPrice", "POST")]
    [InlineData("GetFilteredWarehouses", "GET")]
    [InlineData("GetFilteredWarehouses", "POST")]
    [InlineData("GetAllOrderSources", "POST")]
    [InlineData("GetMainWarehouses", "GET")]
    [InlineData("GetMainWarehouses", "POST")]
    [InlineData("GetSubWarehouses", "GET")]
    [InlineData("GetSubWarehouses", "POST")]
    [InlineData("GetCampaignsByCountry", "GET")]
    [InlineData("GetCampaignsByCountry", "POST")]
    [InlineData("GetAssignableUsers", "GET")]
    public void LegacyRouteIsPublished(string route, string method)
    {
        var attributes = typeof(DataListController).GetMethods()
            .SelectMany(info => info.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>());
        Assert.Contains(attributes, attribute =>
            attribute.HttpMethods.Contains(method) &&
            string.Equals(attribute.Template, route, StringComparison.Ordinal));
    }
}
