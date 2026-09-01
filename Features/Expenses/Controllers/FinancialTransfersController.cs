using Luxira.Api.Data;
using Luxira.Api.Features.Orders.Services;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Administrator")]
[Route("api/v1/financials/transfers")]
[Route("FinancialTransfers")]
[Route("Financial")]
public class FinancialTransfersController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly OrderService _orderService;

    public FinancialTransfersController(ApplicationDbContext context, OrderService orderService)
    {
        _context = context;
        _orderService = orderService;
    }

    [HttpGet]
    [HttpGet("GetTransfers")]
    public async Task<ActionResult<BankTransferListResult>> GetTransfers(
        [FromQuery] int? country,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Where(order => order.IsPaid);

        if (country.HasValue)
        {
            query = query.Where(order => order.Country == country.Value);
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var totalCount = await query.CountAsync(ct);
        var totalAmount = await query.SumAsync(order => (decimal?)order.TotalPrice, ct) ?? 0m;
        var items = await query
            .OrderByDescending(order => order.CreatedDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(order => new BankTransferOrderDto(
                order.Id,
                order.CustomerName,
                order.TelephoneNumber,
                order.Country,
                order.TotalPrice,
                order.PaymentReceiptUrl,
                order.PaymentReceiptS3Key,
                order.CreatedDate))
            .ToListAsync(ct);

        return Ok(new BankTransferListResult(items, totalCount, totalAmount, page, pageSize));
    }

    [HttpPost]
    [HttpPost("CreateTransfer")]
    public async Task<IActionResult> MarkAsBankTransfer(
        [FromBody] MarkBankTransferRequest request,
        CancellationToken ct)
    {
        var order = await _orderService.MarkAsBankTransferAsync(
            request.OrderId,
            User.GetUserId() ?? "system",
            ct);
        return Ok(order);
    }
}

public record MarkBankTransferRequest(int OrderId);
public record BankTransferOrderDto(
    int OrderId,
    string CustomerName,
    string TelephoneNumber,
    int Country,
    decimal Amount,
    string? PaymentReceiptUrl,
    string? PaymentReceiptS3Key,
    DateTime CreatedAt);
public record BankTransferListResult(
    IReadOnlyList<BankTransferOrderDto> Items,
    int TotalCount,
    decimal TotalAmount,
    int Page,
    int PageSize);
