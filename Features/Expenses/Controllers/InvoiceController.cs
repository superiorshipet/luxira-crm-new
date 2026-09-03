using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Data;
using System.Text.Encodings.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/financials/invoices")]
[Route("Invoice")]
public class InvoiceController : ControllerBase
{
    private static readonly string[] Templates =
    [
        "Flare", "LoxxKing", "LotusBlue", "HayatMakeup",
        "Airobics", "Lava", "Liora", "FlareClean"
    ];

    private readonly OrderService _orderService;
    private readonly ApplicationDbContext _context;

    public InvoiceController(OrderService orderService, ApplicationDbContext context)
    {
        _orderService = orderService;
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetInvoices")]
    public IActionResult GetInvoiceTemplates() => Ok(Templates);

    [HttpGet("/Invoice/FlareInvoice")]
    public Task<IActionResult> FlareInvoice(int? storeId, DateTime? startDate, DateTime? endDate, CancellationToken ct) => Render("Flare", storeId, startDate, endDate, ct);
    [HttpGet("/Invoice/LoxxKingInvoice")]
    public Task<IActionResult> LoxxKingInvoice(int? storeId, DateTime? startDate, DateTime? endDate, CancellationToken ct) => Render("LoxxKing", storeId, startDate, endDate, ct);
    [HttpGet("/Invoice/LotusBlueInvoice")]
    public Task<IActionResult> LotusBlueInvoice(int? storeId, DateTime? startDate, DateTime? endDate, CancellationToken ct) => Render("LotusBlue", storeId, startDate, endDate, ct);
    [HttpGet("/Invoice/HayatMakeupInvoice")]
    public Task<IActionResult> HayatMakeupInvoice(int? storeId, DateTime? startDate, DateTime? endDate, CancellationToken ct) => Render("HayatMakeup", storeId, startDate, endDate, ct);
    [HttpGet("/Invoice/AirobicsInvoice")]
    public Task<IActionResult> AirobicsInvoice(int? storeId, DateTime? startDate, DateTime? endDate, CancellationToken ct) => Render("Airobics", storeId, startDate, endDate, ct);
    [HttpGet("/Invoice/LavaInvoice")]
    public Task<IActionResult> LavaInvoice(int? storeId, DateTime? startDate, DateTime? endDate, CancellationToken ct) => Render("Lava", storeId, startDate, endDate, ct);
    [HttpGet("/Invoice/LioraInvoice")]
    public Task<IActionResult> LioraInvoice(int? storeId, DateTime? startDate, DateTime? endDate, CancellationToken ct) => Render("Liora", storeId, startDate, endDate, ct);
    [HttpGet("/Invoice/FlareInvoiceClean")]
    public Task<IActionResult> FlareInvoiceClean(int? storeId, DateTime? startDate, DateTime? endDate, CancellationToken ct) => Render("FlareClean", storeId, startDate, endDate, ct);

    private async Task<IActionResult> Render(string template, int? storeId, DateTime? startDate, DateTime? endDate, CancellationToken ct)
    {
        var encoder = HtmlEncoder.Default;
        if (storeId is not > 0)
            return Content($"<!doctype html><html dir='rtl'><meta charset='utf-8'><title>{encoder.Encode(template)}</title><body><main><h1>فاتورة المبيعات</h1><p>اختر متجرًا لعرض بيانات الفاتورة.</p></main></body></html>", "text/html; charset=utf-8");
        var store = await _context.ManufacturingCompanies.AsNoTracking().FirstOrDefaultAsync(item => item.Id == storeId, ct);
        if (store is null) return NotFound("Store not found.");
        var now = DateTime.Now;
        var start = (startDate ?? (now.TimeOfDay < new TimeSpan(10, 30, 0) ? now.Date.AddDays(-1) : now.Date)).Date.AddHours(10).AddMinutes(30);
        var end = (endDate?.Date.AddHours(10).AddMinutes(30)) ?? start.AddDays(1);
        if (end <= start) end = start.AddDays(1);
        var rows = await _context.Orders.AsNoTracking()
            .Where(order => order.ManufacturingCompanyId == storeId && order.InstantAddedDate >= start && order.InstantAddedDate < end)
            .GroupBy(order => order.Country)
            .Select(group => new
            {
                Country = group.Key, Count = group.Count(), Products = group.Sum(order => order.OrderWarehouses.Sum(item => item.Amount)),
                Net = group.Sum(order => order.TotalPrice - order.DeliveryPrice)
            }).OrderByDescending(item => item.Count).ToListAsync(ct);
        var bodyRows = string.Join(string.Empty, rows.Select(item => $"<tr><td>{item.Country}</td><td>{item.Count}</td><td>{item.Products}</td><td>{item.Net:N2}</td></tr>"));
        var total = rows.Sum(item => item.Net);
        var html = $"<!doctype html><html dir='rtl'><head><meta charset='utf-8'><title>فاتورة {encoder.Encode(store.Name)}</title>" +
            "<style>body{font-family:Arial,sans-serif;margin:32px;color:#18202a}header{display:flex;justify-content:space-between}table{width:100%;border-collapse:collapse;margin-top:24px}th,td{padding:10px;border:1px solid #ccd3dc;text-align:right}th{background:#f2f4f7}</style></head><body>" +
            $"<header><h1>فاتورة المبيعات - {encoder.Encode(store.Name)}</h1><strong>{encoder.Encode(template)}</strong></header><p>من {start:yyyy-MM-dd HH:mm} إلى {end:yyyy-MM-dd HH:mm}</p>" +
            $"<table><thead><tr><th>الدولة</th><th>الطلبات</th><th>المنتجات</th><th>صافي المبيعات</th></tr></thead><tbody>{bodyRows}</tbody></table><h2>الإجمالي: {total:N2}</h2></body></html>";
        return Content(html, "text/html; charset=utf-8");
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<InvoicePreviewDto>> CreatePreview(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken ct)
    {
        var template = Templates.FirstOrDefault(item =>
            string.Equals(item, request.Template, StringComparison.OrdinalIgnoreCase)) ?? "LotusBlue";
        var order = await _orderService.GetOrderByIdAsync(request.OrderId, ct);
        return Ok(new InvoicePreviewDto(template, order));
    }
}

public record CreateInvoiceRequest(int OrderId, string? Template);
public record InvoicePreviewDto(string Template, OrderDto Order);
