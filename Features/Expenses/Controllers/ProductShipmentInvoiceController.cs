using Luxira.Api.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/financials/shipment-invoices")]
[Route("ProductShipmentInvoice")]
public class ProductShipmentInvoiceController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public ProductShipmentInvoiceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpPost("price-offer")]
    [HttpPost("/ProductShipmentInvoice/CreatePriceOffer")]
    public IActionResult CreatePriceOffer([FromBody] CreatePriceOfferRequest request)
    {
        var totalAmount = request.Products.Sum(p => p.Price * p.Quantity);
        var invoiceId = new Random().Next(1000, 10000);

        var html = $@"<!DOCTYPE html>
<html dir='rtl'>
<head><meta charset='utf-8'><title>عرض سعر شحن #{invoiceId}</title>
<style>body {{ font-family: Arial; padding: 20px; }} table {{ width: 100%; border-collapse: collapse; }} th, td {{ border: 1px solid #333; padding: 8px; text-align: right; }}</style>
</head>
<body>
  <h2>عرض سعر شحن #{invoiceId}</h2>
  <p>شركة الشحن: {request.DeliveryCompanyName}</p>
  <p>التاريخ: {DateTime.UtcNow:yyyy-MM-dd}</p>
  <table>
    <thead><tr><th>المنتج</th><th>الكمية</th><th>السعر</th><th>الإجمالي</th></tr></thead>
    <tbody>
      {string.Join("", request.Products.Select(p => $"<tr><td>{p.Name}</td><td>{p.Quantity}</td><td>{p.Price:N0}</td><td>{(p.Quantity * p.Price):N0}</td></tr>"))}
    </tbody>
  </table>
  <h3>المجموع الكلي: {totalAmount:N0}</h3>
</body></html>";

        return Content(html, "text/html");
    }

    [HttpGet("/ProductShipmentInvoice/CreatePriceOffer")]
    public IActionResult CreatePriceOfferForm() => Ok(new { products = Array.Empty<object>() });
}

public record CreatePriceOfferRequest(string DeliveryCompanyName, List<PriceOfferItem> Products);
public record PriceOfferItem(string Name, int Quantity, decimal Price);
