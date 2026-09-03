using System.Security.Claims;
using Luxira.Api.Features.Expenses.DTOs;
using Luxira.Api.Features.Expenses.Services;
using Luxira.Api.Data;
using Luxira.Api.Features.Expenses.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Luxira.Api.Features.Expenses.Controllers;

[ApiController]
[Authorize]
[Route("api/v1/expenses")]
[Route("api/[controller]")]
public class ExpenseController : ControllerBase
{
    private readonly ExpenseService _service;
    private readonly ApplicationDbContext _context;

    public ExpenseController(ExpenseService service, ApplicationDbContext context)
    {
        _service = service;
        _context = context;
    }

    [HttpGet]
    [HttpGet("/Expense/Index")]
    [HttpPost("/Expense/Index")]
    [HttpGet("/Expense/GetExpenses")]
    public async Task<ActionResult<List<ExpenseDto>>> GetExpenses([FromQuery] ExpenseFilterRequest filter, CancellationToken ct)
    {
        var result = await _service.GetExpensesAsync(filter, ct);
        return Ok(result);
    }

    [HttpGet("/Expense/Create")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public IActionResult Create() => Ok(new { createdDate = DateTime.UtcNow.Date });

    [HttpGet("/Expense/Edit")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> Edit([FromQuery] int? id, CancellationToken ct)
    {
        if (!id.HasValue) return NotFound();
        var expense = await _context.Expenses.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return expense is null ? NotFound() : Ok(expense);
    }

    [HttpPost("/Expense/Edit")]
    [Authorize(Roles = "Admin,Administrator,Accountant,ExecutiveDirector")]
    public async Task<IActionResult> Edit([FromQuery] int id, [FromBody] EditExpenseRequest request, CancellationToken ct)
    {
        if (request.Id != id) return NotFound();
        var expense = await _context.Expenses.FirstOrDefaultAsync(item => item.Id == id, ct);
        if (expense is null) return NotFound();
        expense.Description = request.Description.Trim();
        expense.Amount = request.Amount;
        expense.CreatedDate = request.CreatedDate;
        await _context.SaveChangesAsync(ct);
        return Ok(expense);
    }

    [HttpPost("/Expense/DeleteConfirmed")]
    [HttpPost("/Expense/Delete")]
    [Authorize(Roles = "Admin,Administrator,Accountant")]
    public async Task<IActionResult> DeleteConfirmed([FromForm] int id, CancellationToken ct)
    {
        var deleted = await _context.Expenses.Where(item => item.Id == id).ExecuteDeleteAsync(ct);
        return deleted == 0 ? NotFound() : Ok(new { success = true });
    }

    [HttpPost("/Expense/DeleteAll")]
    [Authorize(Roles = "Admin,Administrator,Accountant")]
    public async Task<IActionResult> DeleteAll(CancellationToken ct)
    {
        var deleted = await _context.Expenses.ExecuteDeleteAsync(ct);
        return Ok(new { success = true, deleted });
    }

    [HttpGet("/Expense/Filter")]
    public IActionResult Filter() => Ok(new { });

    [HttpPost("/Expense/Filter")]
    public async Task<IActionResult> Filter([FromForm] int selectedMonth, [FromForm] int selectedYear, [FromForm] int selectedDay, CancellationToken ct)
    {
        var expenses = await _context.Expenses.AsNoTracking()
            .Where(item => item.CreatedDate.Day == selectedDay && item.CreatedDate.Month == selectedMonth && item.CreatedDate.Year == selectedYear)
            .OrderByDescending(item => item.CreatedDate).ToListAsync(ct);
        return Ok(expenses);
    }

    [HttpPost]
    [HttpPost("/Expense/Create")]
    public async Task<ActionResult<ExpenseDto>> CreateExpense([FromBody] CreateExpenseRequest request, CancellationToken ct)
    {
        var userId = Luxira.Api.Utils.Extensions.ClaimsPrincipalExtensions.GetUserId(User) ?? "system";
        var result = await _service.CreateExpenseAsync(request, userId, ct);
        return Ok(result);
    }

    [HttpGet("exchange-rates")]
    [HttpGet("/ExchangeRate/GetRates")]
    [HttpPost("/ExchangeRate/Index")]
    public async Task<ActionResult<List<ExchangeRateDto>>> GetExchangeRates(CancellationToken ct)
    {
        var rates = await _service.GetExchangeRatesAsync(ct);
        return Ok(rates);
    }

    [HttpPost("exchange-rates")]
    [HttpPost("/ExchangeRate/Update")]
    public async Task<IActionResult> UpdateExchangeRate([FromBody] UpdateExchangeRateRequest request, CancellationToken ct)
    {
        await _service.UpdateExchangeRateAsync(request, ct);
        return Ok(new { message = "Exchange rate updated successfully." });
    }

    [HttpGet("/ExchangeRate/Create")]
    public IActionResult CreateExchangeRate() => Ok(new { });

    [HttpGet("/ExchangeRate/Edit")]
    public async Task<IActionResult> EditExchangeRate([FromQuery] int? id, CancellationToken ct)
    {
        if (!id.HasValue) return NotFound();
        var rate = await _context.ExchangeRates.AsNoTracking().FirstOrDefaultAsync(item => item.Id == id, ct);
        return rate is null ? NotFound() : Ok(rate);
    }
}

public sealed record EditExpenseRequest(int Id, string Description, decimal Amount, DateTime CreatedDate);
