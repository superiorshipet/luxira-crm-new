using Luxira.Api.Features.Orders.DTOs;
using Luxira.Api.Features.Orders.Services;
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

    public InvoiceController(OrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpGet]
    [HttpGet("GetInvoices")]
    public IActionResult GetInvoiceTemplates() => Ok(Templates);

    [HttpGet("/Invoice/FlareInvoice")]
    public IActionResult FlareInvoice() => Template("Flare");
    [HttpGet("/Invoice/LoxxKingInvoice")]
    public IActionResult LoxxKingInvoice() => Template("LoxxKing");
    [HttpGet("/Invoice/LotusBlueInvoice")]
    public IActionResult LotusBlueInvoice() => Template("LotusBlue");
    [HttpGet("/Invoice/HayatMakeupInvoice")]
    public IActionResult HayatMakeupInvoice() => Template("HayatMakeup");
    [HttpGet("/Invoice/AirobicsInvoice")]
    public IActionResult AirobicsInvoice() => Template("Airobics");
    [HttpGet("/Invoice/LavaInvoice")]
    public IActionResult LavaInvoice() => Template("Lava");
    [HttpGet("/Invoice/LioraInvoice")]
    public IActionResult LioraInvoice() => Template("Liora");
    [HttpGet("/Invoice/FlareInvoiceClean")]
    public IActionResult FlareInvoiceClean() => Template("FlareClean");

    private OkObjectResult Template(string name) => Ok(new { template = name });

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
