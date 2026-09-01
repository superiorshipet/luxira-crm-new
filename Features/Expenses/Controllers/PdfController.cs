using Luxira.Api.Data;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/financials/pdf")]
[Route("Pdf")]
public class PdfController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public PdfController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet("order/{orderId:int}")]
    [HttpGet("GenerateOrderPdf/{orderId:int}")]
    public async Task<IActionResult> GenerateOrderPdf([FromRoute] int orderId, CancellationToken ct)
    {
        var order = await _context.Orders
            .Include(o => o.OrderWarehouses)
            .Include(o => o.DeliveryCompany)
            .AsNoTracking()
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null)
        {
            throw new NotFoundException($"Order {orderId} not found.");
        }

        // Return a mock HTML/PDF receipt preview for fast execution
        var receiptHtml = $@"<!DOCTYPE html>
<html>
<head><meta charset='utf-8'><title>Order Receipt #{order.Id}</title></head>
<body style='font-family: Arial, sans-serif; padding: 20px; direction: rtl;'>
  <h2>وصل استلام طلب رقم #{order.Id}</h2>
  <p><strong>اسم العميل:</strong> {order.CustomerName}</p>
  <p><strong>رقم الهاتف:</strong> {order.TelephoneNumber}</p>
  <p><strong>العنوان:</strong> {order.Address}</p>
  <p><strong>المبلغ الكلي:</strong> {order.TotalPrice:N0} د.ع</p>
  <p><strong>شركة الشحن:</strong> {order.DeliveryCompany?.Name ?? "-"}</p>
</body>
</html>";

        return Content(receiptHtml, "text/html");
    }
}
