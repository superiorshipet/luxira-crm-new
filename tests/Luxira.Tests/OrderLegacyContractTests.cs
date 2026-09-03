using Luxira.Api.Features.Orders.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Luxira.Tests;

public sealed class OrderLegacyContractTests
{
    [Theory]
    [InlineData(nameof(ExternalOrderApiController.UpdateStatus), "POST", "/Api/Order/UpdateStatus")]
    [InlineData(nameof(ExternalOrderApiController.ShipmentTracking), "GET", "/Api/Order/ShipmentTracking/{orderId:int}")]
    public void ExternalOrderApiRoutesArePublished(string methodName, string verb, string template)
    {
        var attributes = typeof(ExternalOrderApiController).GetMethod(methodName)!
            .GetCustomAttributes(inherit: true).OfType<Microsoft.AspNetCore.Mvc.Routing.HttpMethodAttribute>();
        Assert.Contains(attributes, attribute => attribute.HttpMethods.Contains(verb) && attribute.Template == template);
    }

    [Theory]
    [InlineData(nameof(OrderPostsController.Edit), "POST", "Edit")]
    [InlineData(nameof(OrderPostsController.DeleteImage), "POST", "DeleteImage")]
    [InlineData(nameof(OrderPostsController.Image), "GET", "Image")]
    [InlineData(nameof(OrderPostsController.Delete), "POST", "Delete")]
    [InlineData(nameof(OrderPostsController.Panels), "GET", "Panels")]
    [InlineData(nameof(OrderPostsController.OrderCounts), "GET", "OrderCounts")]
    public void OrderPostLegacyRoutesArePublished(string methodName, string verb, string template)
    {
        var attributes = typeof(OrderPostsController).GetMethod(methodName)!
            .GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>();
        Assert.Contains(attributes, attribute => attribute.HttpMethods.Contains(verb) && attribute.Template == template);
    }

    [Theory]
    [InlineData(nameof(OrderMetaActionsController.Save), "POST", "/OrderMetaActions/Save")]
    [InlineData(nameof(OrderMetaActionsController.Summary), "GET", "/OrderMetaActions/Summary")]
    [InlineData(nameof(OrderMetaActionsController.RatingSummary), "GET", "/OrderMetaActions/RatingSummary")]
    [InlineData(nameof(OrderMetaActionsController.RatingDetails), "GET", "/OrderMetaActions/RatingDetails")]
    [InlineData(nameof(OrderMetaActionsController.ReasonStats), "GET", "/OrderMetaActions/ReasonStats")]
    [InlineData(nameof(OrderMetaActionsController.AllLogs), "GET", "/OrderMetaActions/AllLogs")]
    public void OrderMetaActionLegacyRoutesArePublished(string methodName, string verb, string template)
    {
        var attributes = typeof(OrderMetaActionsController).GetMethod(methodName)!
            .GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>();
        Assert.Contains(attributes, attribute => attribute.HttpMethods.Contains(verb) && attribute.Template == template);
    }

    [Theory]
    [InlineData(nameof(UrgentReportController.MarkAsUnderReview), "POST", "MarkAsUnderReview")]
    [InlineData(nameof(UrgentReportController.MarkAsResolved), "POST", "MarkAsResolved")]
    [InlineData(nameof(UrgentReportController.GetPendingReports), "GET", "GetPendingReports")]
    [InlineData(nameof(UrgentReportController.GetAllReports), "GET", "GetAllReports")]
    public void UrgentReportLegacyRoutesArePublished(string methodName, string verb, string template)
    {
        var attributes = typeof(UrgentReportController).GetMethod(methodName)!
            .GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>();
        Assert.Contains(attributes, attribute => attribute.HttpMethods.Contains(verb) && attribute.Template == template);
    }

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
