using Luxira.Api.Features.ManufacturingCompanies.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Luxira.Tests;

public sealed class ProductImagesLegacyContractTests
{
    [Theory]
    [InlineData("ViewImage", "GET")]
    [InlineData("DeleteProductImageAjax", "POST")]
    [InlineData("UpdateProductImageAjax", "POST")]
    [InlineData("TogglePinProductImageAjax", "POST")]
    [InlineData("TrackProductImageCopyAjax", "POST")]
    [InlineData("CreateImage", "GET")]
    [InlineData("CreateImage", "POST")]
    [InlineData("CreateImageAjax", "POST")]
    [InlineData("UpdateTempImage", "POST")]
    [InlineData("UpdateTempImageAjax", "POST")]
    [InlineData("DeleteTempImage", "POST")]
    [InlineData("DeleteTempImageAjax", "POST")]
    [InlineData("ApproveAll", "POST")]
    [InlineData("ApproveAllAjax", "POST")]
    [InlineData("DeleteAll", "POST")]
    [InlineData("DeleteAllAjax", "POST")]
    [InlineData("CreateVideo", "GET")]
    [InlineData("CreateVideo", "POST")]
    [InlineData("CreateVideoAjax", "POST")]
    [InlineData("UpdateTempVideo", "POST")]
    [InlineData("UpdateTempVideoAjax", "POST")]
    [InlineData("DeleteTempVideo", "POST")]
    [InlineData("DeleteTempVideoAjax", "POST")]
    [InlineData("SaveAllTempVideos", "POST")]
    [InlineData("SaveAllTempVideosAjax", "POST")]
    public void LegacyRouteIsPublished(string route, string method)
    {
        var attributes = typeof(ProductImagesController).GetMethods()
            .SelectMany(info => info.GetCustomAttributes(inherit: true).OfType<HttpMethodAttribute>());
        Assert.Contains(attributes, attribute =>
            attribute.HttpMethods.Contains(method) &&
            string.Equals(attribute.Template, route, StringComparison.Ordinal));
    }
}
