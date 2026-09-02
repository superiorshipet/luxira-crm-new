using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/transactions")]
[Route("EmployeeTransactions")]
public class EmployeeTransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public EmployeeTransactionsController(ApplicationDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    [HttpGet("GetTransactions")]
    public async Task<ActionResult<List<EmployeeTransaction>>> GetTransactions(
        [FromQuery] int? employeeId,
        [FromQuery] string? type,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var query = _context.EmployeeTransactions
            .AsNoTracking()
            .AsQueryable();

        if (employeeId.HasValue && employeeId.Value > 0)
        {
            query = query.Where(t => t.EmployeeId == employeeId.Value);
        }

        if (!string.IsNullOrWhiteSpace(type))
        {
            if (!TryParseTransactionType(type, out var parsedType))
                throw new BadRequestException("Unsupported transaction type.");
            query = query.Where(t => t.TransactionType == parsedType);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.Date >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(t => t.Date <= toDate.Value);
        }

        var list = await query.OrderByDescending(t => t.Date).Take(200).ToListAsync(ct);
        return Ok(list);
    }

    [HttpPost]
    [HttpPost("Create")]
    public async Task<ActionResult<EmployeeTransaction>> CreateTransaction([FromBody] CreateEmployeeTransactionRequest request, CancellationToken ct)
    {
        var employee = await _context.Employees.FindAsync([request.EmployeeId], ct);
        if (employee == null)
        {
            throw new NotFoundException($"Employee {request.EmployeeId} not found.");
        }

        var trans = new EmployeeTransaction
        {
            EmployeeId = request.EmployeeId,
            Amount = request.Amount,
            TransactionType = ParseTransactionType(request.TransactionType),
            Reason = request.Note,
            Date = DateTime.UtcNow
        };

        await _context.EmployeeTransactions.AddAsync(trans, ct);
        await _context.SaveChangesAsync(ct);

        return Ok(trans);
    }

    private static EmployeeTransactionType ParseTransactionType(string value) =>
        TryParseTransactionType(value, out var result)
            ? result
            : throw new BadRequestException("Unsupported transaction type.");

    private static bool TryParseTransactionType(
        string? value,
        out EmployeeTransactionType result)
    {
        result = value?.Trim() switch
        {
            "خصم" or "Deduction" => EmployeeTransactionType.Deduction,
            "مكافأة" or "Bonus" => EmployeeTransactionType.Bonus,
            "سلفة" or "Advance" => EmployeeTransactionType.Advance,
            "ساعات_دوام_إضافية" or "Overtime" => EmployeeTransactionType.Overtime,
            _ => (EmployeeTransactionType)(-1),
        };
        return (int)result >= 0;
    }
}

public record CreateEmployeeTransactionRequest(int EmployeeId, decimal Amount, string TransactionType, string? Note);
