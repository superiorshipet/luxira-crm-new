using Luxira.Api.Features.Orders.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Luxira.Tests;

public sealed class OrderLegacyContractTests
{
    [Theory]
    [InlineData("/Order/Details", "GET")]
    [InlineData("/Order/Details", "POST")]
    [InlineData("/Order/DetailsPartial", "GET")]
    [InlineData("/Order/PostponeOrder", "POST")]
    [InlineData("/Order/HideOrder", "POST")]
    [InlineData("/Order/SetSpecial", "POST")]
    [InlineData("/Order/SetIsComplaints", "POST")]
    [InlineData("/Order/SetBonusPaidForEmployee", "POST")]
    [InlineData("/Order/GetAllOrderIdsForEmployeeBonus", "GET")]
    [InlineData("/Order/RemoveWarehouse", "POST")]
    public void LegacyRouteIsPublished(string route, string method)
    {
        var attributes = typeof(OrderController).GetMethods()
            .SelectMany(info => info.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>());

        Assert.Contains(attributes, attribute =>
            attribute.HttpMethods.Contains(method) &&
            string.Equals(attribute.Template, route, StringComparison.Ordinal));
    }
}
