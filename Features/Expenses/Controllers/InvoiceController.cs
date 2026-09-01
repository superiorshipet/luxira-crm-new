using Luxira.Api.Data;
using Luxira.Api.Features.Expenses.Models;
using Luxira.Api.Utils.Exceptions;
using Luxira.Api.Utils.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/financials/invoices")]
[Route("Invoice")]
public class InvoiceController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public InvoiceController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetInvoices")]
    public async Task<ActionResult<List<InvoiceDto>>> GetInvoices([FromQuery] int? country, CancellationToken ct)
    {
        var query = _context.Invoices.AsNoTracking().AsQueryable();
        if (country.HasValue && country.Value > 0)
        {
            query = query.Where(i => i.Country == country.Value);
        }

        var list = await query.OrderByDescending(i => i.CreatedAt)
            .Select(i => new InvoiceDto(i.Id, i.InvoiceNumber, i.CustomerName, i.TotalAmount, i.DiscountAmount, i.FinalAmount, i.Country, i.CreatedByUserId, i.CreatedAt, i.PdfUrl))
            .ToListAsync(ct);

        return Ok(list);
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<InvoiceDto>> CreateInvoice([FromBody] CreateInvoiceRequest request, CancellationToken ct)
    {
        var invNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
        var final = request.TotalAmount - (request.DiscountAmount ?? 0);

        var inv = new Invoice
        {
            InvoiceNumber = invNumber,
            CustomerName = request.CustomerName,
            TotalAmount = request.TotalAmount,
            DiscountAmount = request.DiscountAmount ?? 0,
            FinalAmount = final,
            Country = request.Country,
            CreatedByUserId = User.GetUserId() ?? "system",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Invoices.AddAsync(inv, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(new InvoiceDto(inv.Id, inv.InvoiceNumber, inv.CustomerName, inv.TotalAmount, inv.DiscountAmount, inv.FinalAmount, inv.Country, inv.CreatedByUserId, inv.CreatedAt, inv.PdfUrl));
    }
}

public record InvoiceDto(int Id, string InvoiceNumber, string CustomerName, decimal TotalAmount, decimal DiscountAmount, decimal FinalAmount, int Country, string CreatedByUserId, DateTime CreatedAt, string? PdfUrl);
public record CreateInvoiceRequest(string CustomerName, decimal TotalAmount, decimal? DiscountAmount, int Country);
