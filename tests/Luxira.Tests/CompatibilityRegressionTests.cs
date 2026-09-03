using Luxira.Api.Infrastructure.Pdf;
using Luxira.Api.Features.Orders.Controllers;
using Luxira.Api.Utils.Binding;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;

namespace Luxira.Tests;

public sealed class CompatibilityRegressionTests
{
    [Theory]
    [InlineData(nameof(OrderController.GetBankTransferApprovals), "GET", "/Order/GetBankTransferApprovals")]
    [InlineData(nameof(OrderController.ConfirmBankTransfer), "POST", "/Order/ConfirmBankTransfer/{id:int}")]
    [InlineData(nameof(OrderController.FlagBankTransferNotReceived), "POST", "/Order/FlagBankTransferNotReceived/{id:int}")]
    [InlineData(nameof(OrderController.RejectBankTransfer), "POST", "/Order/RejectBankTransfer/{id:int}")]
    [InlineData(nameof(OrderController.ApproveBankTransfer), "POST", "/Order/ApproveBankTransfer/{id:int}")]
    [InlineData(nameof(OrderController.ValidateBankTransferChange), "GET", "/Order/ValidateBankTransferChange/{id:int}")]
    [InlineData(nameof(OrderController.SetIsPaid), "POST", "/Order/SetIsPaid/{id:int}")]
    public void Bank_transfer_legacy_routes_are_preserved(string methodName, string verb, string route)
    {
        var method = typeof(OrderController).GetMethod(methodName)!;
        var templates = verb == "GET"
            ? method.GetCustomAttributes(typeof(HttpGetAttribute), inherit: true).Cast<HttpGetAttribute>().Select(attribute => attribute.Template)
            : method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Cast<HttpPostAttribute>().Select(attribute => attribute.Template);

        Assert.Contains(route, templates);
    }

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
