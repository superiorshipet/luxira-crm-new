using System.Text;
using Luxira.Api.Data;
using Luxira.Api.Infrastructure.Pdf;
using Luxira.Api.Features.Orders.Models;
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
    private readonly LuxiraPdfService _pdfService;

    public PdfController(ApplicationDbContext context, LuxiraPdfService pdfService)
    {
        _context = context;
        _pdfService = pdfService;
    }

    [HttpGet("orders")]
    [HttpGet("/Pdf/PrintOrder")]
    [HttpPost("/Pdf/PrintOrder")]
    [HttpGet("/Order/PrintOrder")]
    [HttpPost("/Order/PrintOrder")]
    [HttpGet("/Order/PrintSelectedOrders")]
    [HttpPost("/Order/PrintSelectedOrders")]
    public async Task<IActionResult> PrintOrders(
        [FromQuery] int[]? ids,
        [FromQuery] string? orderIds,
        [FromQuery] bool downloadPdf = false,
        CancellationToken ct = default)
    {
        var targetIds = new List<int>();
        if (ids != null && ids.Length > 0) targetIds.AddRange(ids);
        if (!string.IsNullOrWhiteSpace(orderIds))
        {
            var parsed = orderIds.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var val) ? val : 0)
                .Where(v => v > 0);
            targetIds.AddRange(parsed);
        }

        if (targetIds.Count == 0)
        {
            throw new BadRequestException("At least one order ID must be provided.");
        }

        var orders = await _context.Orders
            .Include(o => o.OrderWarehouses)
            .Include(o => o.DeliveryCompany)
            .AsNoTracking()
            .Where(o => targetIds.Contains(o.Id))
            .ToListAsync(ct);

        if (downloadPdf)
        {
            var pdfBytes = _pdfService.GenerateOrderReceiptPdf(orders);
            return File(pdfBytes, "application/pdf", $"order_receipts_{DateTime.UtcNow:yyyyMMdd_HHmm}.pdf");
        }

        var htmlBuilder = new StringBuilder();
        htmlBuilder.Append(@"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head>
  <meta charset='utf-8'>
  <title>طباعة وصولات الطلبات</title>
  <style>
    body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; margin: 0; padding: 10px; font-size: 13px; }
    .receipt { border: 1px dashed #333; padding: 15px; margin-bottom: 20px; page-break-after: always; width: 80mm; box-sizing: border-box; }
    .header { text-align: center; border-bottom: 1px solid #000; padding-bottom: 8px; margin-bottom: 10px; }
    .header h2 { margin: 0 0 4px 0; font-size: 16px; }
    .row { display: flex; justify-content: space-between; margin-bottom: 5px; }
    .bold { font-weight: bold; }
    table { width: 100%; border-collapse: collapse; margin-top: 10px; margin-bottom: 10px; }
    th, td { border: 1px solid #ccc; padding: 4px; text-align: right; font-size: 11px; }
    .total-box { border-top: 1px solid #000; padding-top: 6px; font-size: 14px; font-weight: bold; text-align: center; }
  </style>
</head>
<body>");

        foreach (var order in orders)
        {
            htmlBuilder.Append($@"
  <div class='receipt'>
    <div class='header'>
      <h2>وصل طلب #{order.Id}</h2>
      <div>{order.CreatedDate:yyyy-MM-dd HH:mm}</div>
    </div>
    <div class='row'><span class='bold'>العميل:</span> <span>{order.CustomerName}</span></div>
    <div class='row'><span class='bold'>الهاتف:</span> <span dir='ltr'>{order.TelephoneNumber}</span></div>
    <div class='row'><span class='bold'>العنوان:</span> <span>{order.State} - {order.Address}</span></div>
    <div class='row'><span class='bold'>شركة الشحن:</span> <span>{order.DeliveryCompany?.Name ?? "-"}</span></div>
    
    <table>
      <thead>
        <tr><th>الكمية</th><th>السعر</th><th>الإجمالي</th></tr>
      </thead>
      <tbody>");

            foreach (var item in order.OrderWarehouses)
            {
                htmlBuilder.Append($@"<tr><td>{item.Amount}</td><td>{item.UnitPrice:N0}</td><td>{(item.Amount * item.UnitPrice):N0}</td></tr>");
            }

            htmlBuilder.Append($@"
      </tbody>
    </table>

    <div class='total-box'>
      المبلغ المطلوب: {order.TotalPrice:N0} د.ع
    </div>
  </div>");
        }

        htmlBuilder.Append("</body></html>");
        return Content(htmlBuilder.ToString(), "text/html");
    }

    [HttpGet("delivery-manifest/{companyId:int}")]
    [HttpGet("/Pdf/PrintOrdersForDelivery")]
    [HttpPost("/Pdf/PrintOrdersForDelivery")]
    public async Task<IActionResult> PrintDeliveryManifest(
        [RouteOrRequest] int? companyId,
        [FromQuery] int? id,
        [FromQuery] bool downloadPdf = false,
        CancellationToken ct = default)
    {
        int targetCompanyId = companyId ?? id ?? 0;
        var company = await _context.DeliveryCompanies.FirstOrDefaultAsync(d => d.Id == targetCompanyId, ct);
        var orders = await _context.Orders
            .Include(o => o.DeliveryCompany)
            .AsNoTracking()
            .Where(o => o.DeliveryCompanyId == targetCompanyId &&
                        o.OrderStatus == OrderStatusCodes.InDelivery)
            .ToListAsync(ct);

        if (downloadPdf)
        {
            var pdfBytes = _pdfService.GenerateDeliveryManifestPdf(company?.Name ?? "شركة التوصيل", orders);
            return File(pdfBytes, "application/pdf", $"manifest_{targetCompanyId}_{DateTime.UtcNow:yyyyMMdd}.pdf");
        }

        var html = $@"<!DOCTYPE html>
<html dir='rtl' lang='ar'>
<head><meta charset='utf-8'><title>كشف تسليم شركة الشحن</title>
<style>body {{ font-family: Arial; padding: 20px; }} table {{ width: 100%; border-collapse: collapse; }} th, td {{ border: 1px solid #333; padding: 6px; text-align: right; }}</style>
</head>
<body>
  <h2>كشف تسليم شحنات - {company?.Name ?? "شركة التوصيل"} - {DateTime.UtcNow:yyyy-MM-dd}</h2>
  <p>العدد الكلي: {orders.Count} طلب</p>
  <table>
    <thead><tr><th>رقم الطلب</th><th>العميل</th><th>الهاتف</th><th>المحافظة</th><th>المبلغ المطلوب</th></tr></thead>
    <tbody>
      {string.Join("", orders.Select(o => $"<tr><td>#{o.Id}</td><td>{o.CustomerName}</td><td>{o.TelephoneNumber}</td><td>{o.State}</td><td>{o.TotalPrice:N0}</td></tr>"))}
    </tbody>
  </table>
</body></html>";

        return Content(html, "text/html");
    }

    [HttpGet("Index")]
    [HttpPost("Index")]
    public IActionResult Index() => Ok(new
    {
        orderPrintUrl = "/Pdf/PrintOrder",
        deliveryPrintUrl = "/Pdf/PrintOrdersForDelivery",
        financialTransferPrintUrl = "/Pdf/PrintFinancialTransfersReceipts",
        attendancePrintUrl = "/Pdf/PrintAttendanceLog"
    });

    [HttpGet("StoreDailyInvoice")]
    public async Task<IActionResult> StoreDailyInvoice(int? storeId, DateTime? date, CancellationToken ct)
    {
        var targetDate = (date ?? DateTime.UtcNow).Date;
        var end = targetDate.AddDays(1);
        var query = _context.Orders.AsNoTracking().Include(order => order.DeliveryCompany).Include(order => order.OrderWarehouses)
            .Where(order => order.CreatedDate >= targetDate && order.CreatedDate < end);
        if (storeId.HasValue) query = query.Where(order => order.ManufacturingCompanyId == storeId.Value);
        var orders = await query.OrderBy(order => order.Id).ToListAsync(ct);
        return File(_pdfService.GenerateOrderReceiptPdf(orders), "application/pdf", $"store-daily-{targetDate:yyyyMMdd}.pdf");
    }

    [HttpGet("TestEmailInvoices")]
    [HttpGet("TestDeliveryCompanyDailyInvoicesService")]
    [HttpGet("TestDeliveryInvoicesEmail")]
    public IActionResult TestInvoiceServices() => Ok(new
    {
        success = true,
        message = "Invoice PDF services are available.",
        emailDispatchRequiresBackgroundJob = true
    });

    [HttpGet("PrintOrdersForDeliveryDetails")]
    [Authorize(Roles = "Admin,Administrator,ExecutiveDirector,FollowUpDepartment,DeliveryCompany,DeliveryRepresentative")]
    public Task<IActionResult> PrintOrdersForDeliveryDetails(string? ids, CancellationToken ct) =>
        PrintOrders(null, ids, true, ct);

    [HttpGet("PrintFinancialTransfersReceipts")]
    [Authorize(Roles = "Admin,Administrator")]
    public Task<IActionResult> PrintFinancialTransfersReceipts(string? ids, CancellationToken ct) =>
        PrintOrders(null, ids, true, ct);

    [HttpGet("PrintEmployeeTransactionStatement")]
    [Authorize(Roles = "Admin,Administrator,Accountant,Observer,ExecutiveDirector")]
    public async Task<IActionResult> PrintEmployeeTransactionStatement(int id, CancellationToken ct)
    {
        var transaction = await _context.EmployeeTransactions.AsNoTracking().Include(item => item.Employee)
            .FirstOrDefaultAsync(item => item.Id == id, ct);
        if (transaction is null) return NotFound();
        return File(_pdfService.GenerateEmployeeTransactionReceiptPdf(transaction), "application/pdf", $"employee-transaction-{id}.pdf");
    }

    [HttpGet("PrintEmployeeTransactionsStatement")]
    [Authorize(Roles = "Admin,Administrator,Accountant,Observer,ExecutiveDirector")]
    public async Task<IActionResult> PrintEmployeeTransactionsStatement(int employeeId, DateTime? fromDate, DateTime? toDate, CancellationToken ct)
    {
        var query = _context.EmployeeTransactions.AsNoTracking().Include(item => item.Employee)
            .Where(item => item.EmployeeId == employeeId);
        if (fromDate.HasValue) query = query.Where(item => item.Date >= fromDate.Value.Date);
        if (toDate.HasValue) query = query.Where(item => item.Date < toDate.Value.Date.AddDays(1));
        var rows = await query.OrderBy(item => item.Date).ThenBy(item => item.Id).ToListAsync(ct);
        return File(_pdfService.GenerateEmployeeTransactionsStatementPdf(rows), "application/pdf", $"employee-statement-{employeeId}.pdf");
    }

    [HttpGet("AttendancePdfFooter")]
    [HttpPost("AttendancePdfFooter")]
    public IActionResult AttendancePdfFooter() => Content($"Luxira CRM | {DateTime.UtcNow:yyyy-MM-dd HH:mm}", "text/plain");

    [HttpGet("PrintAttendanceLog")]
    [HttpPost("PrintAttendanceLog")]
    public async Task<IActionResult> PrintAttendanceLog(string? ids, CancellationToken ct)
    {
        var parsedIds = ParseIds(ids);
        if (parsedIds.Count == 0) return BadRequest("No attendance IDs provided.");
        var rows = await _context.EmployeeAttendanceLogs.AsNoTracking().Where(item => parsedIds.Contains(item.Id))
            .OrderBy(item => item.CheckInAt).ToListAsync(ct);
        return File(_pdfService.GenerateAttendanceLogPdf(rows), "application/pdf", $"attendance-{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    private static List<int> ParseIds(string? ids) => (ids ?? string.Empty)
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => int.TryParse(value, out var id) ? id : 0).Where(id => id > 0).Distinct().ToList();

    [HttpGet("/Order/PrintSelectedOrdersForDelivery")]
    [HttpPost("/Order/PrintSelectedOrdersForDelivery")]
    [Authorize(Roles = "Admin,Administrator,Accountant,FollowUpDepartment,ExecutiveDirector,DeliveryCompany,DeliveryRepresentative")]
    public Task<IActionResult> PrintSelectedOrdersForDelivery(
        [FromQuery] int[]? ids,
        [FromQuery] string? orderIds,
        [FromQuery] bool downloadPdf = false,
        CancellationToken ct = default) =>
        PrintOrders(ids, orderIds, downloadPdf, ct);
}
