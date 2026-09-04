using Luxira.Api.Data;
using Luxira.Api.Features.Employees.Models;
using Luxira.Api.Utils.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Luxira.Api.Infrastructure.Pdf;
using Luxira.Api.Features.Operations.Models;

namespace Luxira.Api.Features.Employees.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/employees/transactions")]
[Route("EmployeeTransactions")]
public class EmployeeTransactionsController : ControllerBase
{
    private readonly ApplicationDbContext _context;
    private readonly LuxiraPdfService _pdf;

    public EmployeeTransactionsController(ApplicationDbContext context, LuxiraPdfService pdf)
    {
        _context = context;
        _pdf = pdf;
    }

    [HttpGet]
    [HttpGet("GetTransactions")]
    [HttpGet("/EmployeeTransactions/Index")]
    [HttpPost("/EmployeeTransactions/Index")]
    public async Task<ActionResult<List<EmployeeTransaction>>> GetTransactions(
        [FromQuery] int? employeeId,
        [FromQuery] string? type,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        CancellationToken ct)
    {
        var query = _context.EmployeeTransactions
            .AsNoTracking()
            .Where(transaction => !transaction.IsDeleted)
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

    [HttpGet("/EmployeeTransactions/Create")]
    public async Task<IActionResult> Create(CancellationToken ct) => Ok(new
    {
        employees = await _context.Employees.AsNoTracking().Where(employee => employee.IsActive && employee.IsShown)
            .OrderBy(employee => employee.Name).Select(employee => new { employee.Id, Name = employee.DisplayName ?? employee.Name }).ToListAsync(ct),
        transactionTypes = Enum.GetValues<EmployeeTransactionType>().Select(type => new { id = (int)type, name = type.ToString() })
    });

    [HttpGet("/EmployeeTransactions/GetTransactionForEdit")]
    [HttpGet("/EmployeeTransactions/Edit")]
    public async Task<IActionResult> GetTransactionForEdit([FromQuery] int id, CancellationToken ct)
    {
        var transaction = await TransactionQuery().FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, ct);
        return transaction is null ? NotFound() : Ok(transaction);
    }

    [HttpGet("/EmployeeTransactions/TransferReceiptPdf")]
    public async Task<IActionResult> TransferReceiptPdf([FromQuery] int id, CancellationToken ct)
    {
        var transaction = await TransactionQuery().FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, ct);
        return transaction is null
            ? NotFound()
            : File(_pdf.GenerateEmployeeTransactionReceiptPdf(transaction), "application/pdf", $"employee-transaction-{id}.pdf");
    }

    [HttpPost("/EmployeeTransactions/Edit")]
    public Task<IActionResult> Edit([FromForm] int id, [FromForm] decimal amount, [FromForm] string transactionType, [FromForm] string? reason, CancellationToken ct) =>
        UpdateTransaction(id, amount, transactionType, reason, ct);

    [HttpPost("/EmployeeTransactions/UpdateTransactionFromPopup")]
    public Task<IActionResult> UpdateTransactionFromPopup([FromBody] UpdateEmployeeTransactionRequest request, CancellationToken ct) =>
        UpdateTransaction(request.Id, request.Amount, request.TransactionType, request.Reason, ct);

    [HttpGet("/EmployeeTransactions/TransactionEditHistory")]
    public async Task<IActionResult> TransactionEditHistory([FromQuery] int id, CancellationToken ct)
    {
        var transaction = await _context.EmployeeTransactions.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return transaction is null ? NotFound() : Ok(ParseEditHistory(transaction.EditHistoryJson));
    }

    [HttpGet("/EmployeeTransactions/GetTransactionsEditHistory")]
    public async Task<IActionResult> GetTransactionsEditHistory(CancellationToken ct)
    {
        var rows = await TransactionQuery().Where(item => item.EditHistoryJson != null && item.EditHistoryJson != "")
            .OrderByDescending(item => item.Date).Take(1_000).ToListAsync(ct);
        return Ok(rows.Select(item => new { transaction = item, history = ParseEditHistory(item.EditHistoryJson) }));
    }

    [HttpGet("/EmployeeTransactions/GetDeletedTransactions")]
    public async Task<IActionResult> GetDeletedTransactions(CancellationToken ct) => Ok(await TransactionQuery()
        .Where(item => item.IsDeleted).OrderByDescending(item => item.DeletedAt ?? item.Date).Take(1_000).ToListAsync(ct));

    [HttpGet("/EmployeeTransactions/GetPermanentlyDeletedTransactions")]
    public async Task<IActionResult> GetPermanentlyDeletedTransactions(CancellationToken ct)
    {
        var rows = await _context.AppLogs.AsNoTracking().Where(item => item.Category == PermanentDeleteCategory).OrderByDescending(item => item.CreatedAtUtc).Take(1000).Select(item => item.Message).ToListAsync(ct);
        var entries = rows.Select(message => { try { return JsonSerializer.Deserialize<PermanentDeleteEntry>(message); } catch (JsonException) { return null; } }).Where(item => item is not null).Cast<PermanentDeleteEntry>().ToList(); var employeeIds = entries.Select(item => item.EmployeeId).Distinct().ToArray(); var currencies = await _context.Employees.AsNoTracking().Where(item => employeeIds.Contains(item.Id)).Select(item => new { item.Id, item.Country, item.Nationality }).ToDictionaryAsync(item => item.Id, item => Currency(item.Country ?? item.Nationality), ct);
        var items = entries.Select(item => { var currency = string.IsNullOrWhiteSpace(item.Currency) ? currencies.GetValueOrDefault(item.EmployeeId, "") : item.Currency; var amount = item.Amount.ToString("0.##"); return new { transactionId = item.TransactionId, employeeName = string.IsNullOrWhiteSpace(item.EmployeeName) ? "بدون اسم" : item.EmployeeName, amount, amountWithCurrency = string.IsNullOrWhiteSpace(currency) ? amount : amount + " " + currency, currency, transactionType = string.IsNullOrWhiteSpace(item.TransactionType) ? "-" : item.TransactionType, reason = string.IsNullOrWhiteSpace(item.Reason) ? "-" : item.Reason, transactionDate = item.TransactionDate.ToString("yyyy-MM-dd HH:mm:ss"), deletedAt = item.DeletedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-", deletedBy = string.IsNullOrWhiteSpace(item.DeletedBy) ? "-" : item.DeletedBy, permanentlyDeletedAt = item.PermanentlyDeletedAt.ToString("yyyy-MM-dd HH:mm:ss"), permanentlyDeletedBy = string.IsNullOrWhiteSpace(item.PermanentlyDeletedBy) ? "-" : item.PermanentlyDeletedBy }; });
        return Ok(new { success = true, items });
    }

    [HttpGet("/EmployeeTransactions/GetTransactionsByDay")]
    public async Task<IActionResult> GetTransactionsByDay([FromQuery] DateTime? date, [FromQuery] int? employeeId, CancellationToken ct)
    {
        if (!date.HasValue) return BadRequest(new { success = false, message = "يجب تحديد تاريخ صحيح" });
        var end = date.Value.Date.AddDays(1);
        var query = TransactionQuery().Where(item => !item.IsDeleted && item.Date >= date.Value.Date && item.Date < end);
        if (employeeId is > 0) query = query.Where(item => item.EmployeeId == employeeId);
        return Ok(await query.OrderByDescending(item => item.Date).ToListAsync(ct));
    }

    [HttpPost("/EmployeeTransactions/RestoreDeleted")]
    public async Task<IActionResult> RestoreDeleted([FromForm] int id, CancellationToken ct)
    {
        var changed = await _context.EmployeeTransactions.Where(item => item.Id == id && item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsDeleted, false).SetProperty(item => item.DeletedAt, (DateTime?)null).SetProperty(item => item.DeletedByUserName, (string?)null), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    [HttpPost("/EmployeeTransactions/RestoreAllDeleted")]
    public async Task<IActionResult> RestoreAllDeleted(CancellationToken ct)
    {
        var changed = await _context.EmployeeTransactions.Where(item => item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsDeleted, false).SetProperty(item => item.DeletedAt, (DateTime?)null).SetProperty(item => item.DeletedByUserName, (string?)null), ct);
        return Ok(new { success = true, restoredCount = changed });
    }

    [HttpPost("/EmployeeTransactions/DeleteSelected")]
    public async Task<IActionResult> DeleteSelected([FromForm] string ids, CancellationToken ct)
    {
        var selected = ParseIds(ids);
        var changed = await SoftDeleteQuery(selected).ExecuteUpdateAsync(setters => setters
            .SetProperty(item => item.IsDeleted, true)
            .SetProperty(item => item.DeletedAt, DateTime.UtcNow)
            .SetProperty(item => item.DeletedByUserName, User.Identity!.Name), ct);
        return Ok(new { success = true, deletedCount = changed });
    }

    [HttpPost("/EmployeeTransactions/DeleteAllActive")]
    public async Task<IActionResult> DeleteAllActive([FromQuery] int? employeeId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate, CancellationToken ct)
    {
        var query = _context.EmployeeTransactions.Where(item => !item.IsDeleted);
        if (employeeId is > 0) query = query.Where(item => item.EmployeeId == employeeId);
        if (fromDate.HasValue) query = query.Where(item => item.Date >= fromDate.Value);
        if (toDate.HasValue) query = query.Where(item => item.Date < toDate.Value.Date.AddDays(1));
        var changed = await query.ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsDeleted, true)
            .SetProperty(item => item.DeletedAt, DateTime.UtcNow).SetProperty(item => item.DeletedByUserName, User.Identity!.Name), ct);
        return Ok(new { success = true, deletedCount = changed });
    }

    [HttpPost("/EmployeeTransactions/DeleteDeletedPermanently")]
    public async Task<IActionResult> DeleteDeletedPermanently([FromForm] int id, CancellationToken ct)
    {
        var item = await _context.EmployeeTransactions.Include(row => row.Employee).FirstOrDefaultAsync(row => row.Id == id && row.IsDeleted, ct);
        if (item is null) return NotFound();
        AddPermanentDeleteAudit(item); _context.EmployeeTransactions.Remove(item); await _context.SaveChangesAsync(ct);
        return Ok(new { success = true });
    }

    [HttpPost("/EmployeeTransactions/DeleteAllDeletedPermanently")]
    public async Task<IActionResult> DeleteAllDeletedPermanently(CancellationToken ct)
    {
        var items = await _context.EmployeeTransactions.Include(item => item.Employee).Where(item => item.IsDeleted).ToListAsync(ct);
        foreach (var item in items) AddPermanentDeleteAudit(item);
        _context.EmployeeTransactions.RemoveRange(items); await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, deletedCount = items.Count });
    }

    [HttpPost("/EmployeeTransactions/DeleteConfirmed")]
    [HttpPost("/EmployeeTransactions/Delete")]
    public async Task<IActionResult> DeleteConfirmed([FromForm] int id, CancellationToken ct)
    {
        var changed = await _context.EmployeeTransactions.Where(item => item.Id == id && !item.IsDeleted)
            .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.IsDeleted, true)
                .SetProperty(item => item.DeletedAt, DateTime.UtcNow).SetProperty(item => item.DeletedByUserName, User.Identity!.Name), ct);
        return changed == 0 ? NotFound() : Ok(new { success = true });
    }

    private async Task<IActionResult> UpdateTransaction(int id, decimal amount, string type, string? reason, CancellationToken ct)
    {
        var transaction = await _context.EmployeeTransactions.FirstOrDefaultAsync(item => item.Id == id && !item.IsDeleted, ct);
        if (transaction is null) return NotFound();
        var previous = new { transaction.Amount, Type = transaction.TransactionType.ToString(), transaction.Reason, transaction.Date };
        transaction.Amount = amount;
        transaction.TransactionType = ParseTransactionType(type);
        transaction.Reason = reason?.Trim();
        var history = ParseEditHistory(transaction.EditHistoryJson).ToList();
        history.Add(new { changedAt = DateTime.UtcNow, changedBy = User.Identity?.Name, previous, current = new { transaction.Amount, Type = transaction.TransactionType.ToString(), transaction.Reason } });
        transaction.EditHistoryJson = JsonSerializer.Serialize(history);
        await _context.SaveChangesAsync(ct);
        return Ok(new { success = true, transaction });
    }

    private IQueryable<EmployeeTransaction> TransactionQuery() => _context.EmployeeTransactions.AsNoTracking().Include(item => item.Employee);
    private const string PermanentDeleteCategory = "EmployeeTransactionPermanentDelete";
    private void AddPermanentDeleteAudit(EmployeeTransaction item)
    {
        var deletedAt = DateTime.UtcNow; var deletedBy = User.Identity?.Name ?? "Unknown";
        var entry = new PermanentDeleteEntry(item.Id, item.EmployeeId, item.Employee?.DisplayName ?? item.Employee?.Name, item.Amount, item.TransactionType.ToString(), item.Reason, item.Date, item.DeletedAt, item.DeletedByUserName, deletedAt, deletedBy, Currency(item.Employee?.Country ?? item.Employee?.Nationality));
        _context.AppLogs.Add(new AppLog { CreatedAtUtc = deletedAt, Level = "Information", Category = PermanentDeleteCategory, Type = "Audit", Kind = "PermanentDelete", Message = JsonSerializer.Serialize(entry) });
    }
    private IQueryable<EmployeeTransaction> SoftDeleteQuery(List<int> ids) => _context.EmployeeTransactions.Where(item => ids.Contains(item.Id) && !item.IsDeleted);
    private static List<int> ParseIds(string ids) => ids.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(value => int.TryParse(value, out var id) ? id : 0).Where(id => id > 0).Distinct().Take(10_000).ToList();
    private static List<object> ParseEditHistory(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<object>>(json) ?? []; }
        catch (JsonException) { return []; }
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
    private static string Currency(string? country) { var value = (country ?? "").ToLowerInvariant(); if (value.Contains("مصر") || value.Contains("egypt") || value.Contains("egp")) return "EGP"; if (value.Contains("ترك") || value.Contains("turkey") || value.Contains("try")) return "TRY"; if (value.Contains("عراق") || value.Contains("iraq") || value.Contains("iqd")) return "IQD"; if (value.Contains("ليبيا") || value.Contains("libya") || value.Contains("lyd")) return "LYD"; if (value.Contains("الأردن") || value.Contains("اردن") || value.Contains("jordan") || value.Contains("jod")) return "JOD"; if (value.Contains("الكويت") || value.Contains("kuwait") || value.Contains("kwd")) return "KWD"; if (value.Contains("قطر") || value.Contains("qatar") || value.Contains("qar")) return "QAR"; if (value.Contains("عمان") || value.Contains("oman") || value.Contains("omr")) return "OMR"; if (value.Contains("البحرين") || value.Contains("bahrain") || value.Contains("bhd")) return "BHD"; if (value.Contains("تونس") || value.Contains("tunisia") || value.Contains("tnd")) return "TND"; if (value.Contains("السعود") || value.Contains("saudi") || value.Contains("sar")) return "SAR"; if (value.Contains("الإمارات") || value.Contains("الامارات") || value.Contains("emirates") || value.Contains("uae") || value.Contains("aed")) return "AED"; return value.Contains("usd") || value.Contains("دولار") ? "USD" : ""; }
}

public sealed record PermanentDeleteEntry(int TransactionId, int EmployeeId, string? EmployeeName, decimal Amount, string TransactionType, string? Reason, DateTime TransactionDate, DateTime? DeletedAt, string? DeletedBy, DateTime PermanentlyDeletedAt, string PermanentlyDeletedBy, string? Currency = null);

public record CreateEmployeeTransactionRequest(int EmployeeId, decimal Amount, string TransactionType, string? Note);
public record UpdateEmployeeTransactionRequest(int Id, decimal Amount, string TransactionType, string? Reason);
