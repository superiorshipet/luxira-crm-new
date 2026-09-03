using Luxira.Api.Infrastructure.Pdf;
using Luxira.Api.Utils.Binding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Luxira.Tests;

public sealed class CompatibilityRegressionTests
{
    [Fact]
    public void Shipment_price_offer_is_a_real_pdf()
    {
        var bytes = new LuxiraPdfService().GenerateShipmentPriceOfferPdf(
            "شركة اختبار", "العنوان", "0123", "test@example.com", 1234, new DateTime(2026, 9, 4),
            [("منتج", 2, 15.5m)]);

        Assert.True(bytes.Length > 100);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Theory]
    [InlineData(true, "41")]
    [InlineData(false, "42")]
    public async Task Route_or_request_binder_accepts_route_and_query_ids(bool fromRoute, string expected)
    {
        var http = new DefaultHttpContext();
        if (fromRoute) http.Request.RouteValues["id"] = expected;
        else http.Request.QueryString = new QueryString($"?id={expected}");
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor(), new ModelStateDictionary());
        var metadata = new EmptyModelMetadataProvider().GetMetadataForType(typeof(int));
        var bindingContext = DefaultModelBindingContext.CreateBindingContext(
            actionContext, new CompositeValueProvider(), metadata, null, "id");

        await new RouteOrRequestModelBinder().BindModelAsync(bindingContext);

        Assert.True(bindingContext.Result.IsModelSet);
        Assert.Equal(int.Parse(expected), bindingContext.Result.Model);
    }
}
