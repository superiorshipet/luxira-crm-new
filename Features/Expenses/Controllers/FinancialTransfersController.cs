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
[Route("api/v1/financials/transfers")]
[Route("FinancialTransfers")]
[Route("Financial")]
public class FinancialTransfersController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public FinancialTransfersController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetTransfers")]
    public async Task<ActionResult<List<FinancialTransfer>>> GetTransfers(CancellationToken ct)
    {
        var transfers = await _context.FinancialTransfers
            .AsNoTracking()
            .OrderByDescending(t => t.TransferredAt)
            .ToListAsync(ct);

        return Ok(transfers);
    }

    [HttpPost]
    [HttpPost("CreateTransfer")]
    public async Task<ActionResult<FinancialTransfer>> CreateTransfer([FromBody] CreateFinancialTransferRequest request, CancellationToken ct)
    {
        var transfer = new FinancialTransfer
        {
            FromAccount = request.FromAccount,
            ToAccount = request.ToAccount,
            Amount = request.Amount,
            Currency = request.Currency ?? "IQD",
            ExchangeRate = request.ExchangeRate ?? 1.0m,
            TransferredByUserId = User.GetUserId() ?? "system",
            TransferredAt = DateTime.UtcNow,
            Note = request.Note
        };

        await _context.FinancialTransfers.AddAsync(transfer, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(transfer);
    }
}

public record CreateFinancialTransferRequest(string FromAccount, string ToAccount, decimal Amount, string? Currency, decimal? ExchangeRate, string? Note);
