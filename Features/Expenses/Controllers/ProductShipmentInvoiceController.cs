using Luxira.Api.Data;
using Luxira.Api.Infrastructure.Pdf;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/financials/shipment-invoices")]
[Route("ProductShipmentInvoice")]
public class ProductShipmentInvoiceController : ControllerBase
{
    private readonly LuxiraPdfService _pdf;

    public ProductShipmentInvoiceController(LuxiraPdfService pdf)
    {
        _pdf = pdf;
    }

    [HttpPost("/api/v1/financials/shipment-invoices/price-offer")]
    public IActionResult CreatePriceOffer([FromBody] CreatePriceOfferRequest request)
        => CreatePriceOfferCore(request);

    [HttpPost("/ProductShipmentInvoice/CreatePriceOffer")]
    public IActionResult CreatePriceOfferLegacy([FromForm] CreatePriceOfferRequest request)
        => CreatePriceOfferCore(request);

    private IActionResult CreatePriceOfferCore(CreatePriceOfferRequest request)
    {
        if (request.Products.Count == 0) return BadRequest(new { success = false, message = "أضف منتجًا واحدًا على الأقل." });
        var invoiceId = Random.Shared.Next(1000, 10000);
        var products = request.Products.Select(item => (item.Name, item.EffectiveQuantity, item.Price)).ToList();
        var bytes = _pdf.GenerateShipmentPriceOfferPdf(request.DeliveryCompanyName, request.DeliveryCompanyAddress,
            request.DeliveryCompanyPhoneNumber, request.DeliveryCompanyEmail, invoiceId, DateTime.Now, products);
        Response.Headers.ContentDisposition = "inline; filename=OrdersReport.pdf";
        return File(bytes, "application/pdf");
    }

    [HttpGet("/ProductShipmentInvoice/CreatePriceOffer")]
    public IActionResult CreatePriceOfferForm() => Ok(new { products = Array.Empty<object>() });
}

public sealed record CreatePriceOfferRequest(string DeliveryCompanyName, List<PriceOfferItem> Products,
    string? DeliveryCompanyAddress = null, string? DeliveryCompanyPhoneNumber = null, string? DeliveryCompanyEmail = null);
public sealed record PriceOfferItem(string Name, int Quantity, decimal Price, int Amount = 0)
{
    public int EffectiveQuantity => Amount > 0 ? Amount : Quantity;
}
