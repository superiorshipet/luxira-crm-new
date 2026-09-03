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
    [HttpGet("/FinancialTransfers/Index")]
    [HttpPost("/FinancialTransfers/Index")]
    public async Task<ActionResult<BankTransferListResult>> GetTransfers(
        [FromQuery] string? search,
        [FromQuery] int? country,
        [FromQuery] int? storeId,
        [FromQuery] int? deliveryCompanyId,
        [FromQuery] int? dayOfWeek,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] bool showAll = false,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var query = _context.Orders
            .AsNoTracking()
            .Where(order => order.IsPaid);

        if (country.HasValue)
        {
            query = query.Where(order => order.Country == country.Value);
        }

        if (storeId is > 0)
        {
            query = query.Where(order => order.ManufacturingCompanyId == storeId);
        }

        if (deliveryCompanyId is > 0)
        {
            query = query.Where(order => order.DeliveryCompanyId == deliveryCompanyId);
        }

        if (dayOfWeek is >= 0 and <= 6)
        {
            var monday = new DateTime(1900, 1, 1);
            query = query.Where(order => EF.Functions.DateDiffDay(monday, order.CreatedDate) % 7 == dayOfWeek);
        }

        if (startDate.HasValue)
        {
            query = query.Where(order => order.CreatedDate >= startDate.Value.Date);
        }

        if (endDate.HasValue)
        {
            var exclusiveEnd = endDate.Value.Date.AddDays(1);
            query = query.Where(order => order.CreatedDate < exclusiveEnd);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalizedSearch = NormalizeDigits(search.Trim());
            var codeSearch = normalizedSearch.TrimStart('#').Trim();
            if (codeSearch.Length <= 8 && codeSearch.All(char.IsDigit) && int.TryParse(codeSearch, out var orderCode))
            {
                var phoneTerms = BuildPhoneTerms(normalizedSearch);
                query = query.Where(order =>
                    order.Id == orderCode ||
                    order.ExternalOrderId == orderCode ||
                    phoneTerms.Contains(order.TelephoneNumber) ||
                    (order.SecondTelephoneNumber != null && phoneTerms.Contains(order.SecondTelephoneNumber)));
            }
            else if (IsPhoneSearch(normalizedSearch))
            {
                var phoneTerms = BuildPhoneTerms(normalizedSearch);
                query = query.Where(order =>
                    phoneTerms.Contains(order.TelephoneNumber) ||
                    (order.SecondTelephoneNumber != null && phoneTerms.Contains(order.SecondTelephoneNumber)));
            }
            else
            {
                query = query.Where(order =>
                    order.CustomerName == normalizedSearch ||
                    order.SourceName == normalizedSearch ||
                    order.Chaturl == normalizedSearch ||
                    _context.ManufacturingCompanies.Any(company => company.Id == order.ManufacturingCompanyId && company.Name == normalizedSearch) ||
                    _context.DeliveryCompanies.Any(company => company.Id == order.DeliveryCompanyId && (company.DisplayName == normalizedSearch || company.Name == normalizedSearch)));
            }
        }

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);
        var bankTransferTotalCount = await query.CountAsync(ct);
        var bankTransferTotalAmount = await query.SumAsync(order => (decimal?)order.TotalPrice, ct) ?? 0m;

        var listQuery = query;
        if (!showAll && string.IsNullOrWhiteSpace(search))
        {
            listQuery = listQuery.Where(order =>
                order.PaymentReceiptUrl != null &&
                order.PaymentReceiptUrl != "" &&
                !order.PaymentReceiptUrl.Contains("DefaultImage.svg"));
        }

        var totalCount = await listQuery.CountAsync(ct);
        var totalAmount = await listQuery.SumAsync(order => (decimal?)order.TotalPrice, ct) ?? 0m;
        var items = await listQuery
            .OrderByDescending(order => order.CreatedDate)
            .ThenByDescending(order => order.Id)
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

        return Ok(new BankTransferListResult(
            items,
            totalCount,
            totalAmount,
            bankTransferTotalCount,
            bankTransferTotalAmount,
            page,
            pageSize));
    }

    private static bool IsPhoneSearch(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(character =>
                !char.IsDigit(character) && character is not ('+' or '-' or ' ' or '(' or ')' or '\t')))
        {
            return false;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        return digits.Length is >= 5 and <= 20;
    }

    private static string[] BuildPhoneTerms(string value)
    {
        var digits = new string(NormalizeDigits(value).Where(char.IsDigit).ToArray());
        if (digits.Length is < 5 or > 25) return [];

        var terms = new HashSet<string>(StringComparer.Ordinal) { digits };
        var withoutZero = digits.TrimStart('0');
        if (withoutZero.Length >= 5) terms.Add(withoutZero);
        if (!digits.StartsWith('0')) terms.Add('0' + digits);

        foreach (var length in new[] { 12, 11, 10, 9, 8 })
        {
            if (digits.Length <= length) continue;
            var tail = digits[^length..];
            terms.Add(tail);
            var trimmedTail = tail.TrimStart('0');
            if (trimmedTail.Length >= 5) terms.Add(trimmedTail);
            if (!tail.StartsWith('0')) terms.Add('0' + tail);
        }

        return terms.Take(12).ToArray();
    }

    private static string NormalizeDigits(string value) => new(value.Select(character => character switch
    {
        >= '٠' and <= '٩' => (char)('0' + character - '٠'),
        >= '۰' and <= '۹' => (char)('0' + character - '۰'),
        _ => character
    }).ToArray());

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
    int BankTransferTotalCount,
    decimal BankTransferTotalAmount,
    int Page,
    int PageSize);
