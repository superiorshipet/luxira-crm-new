using System.Security.Claims;
using Luxira.Api.Features.Expenses.DTOs;
using Luxira.Api.Features.Expenses.Services;
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

    public ExpenseController(ExpenseService service)
    {
        _service = service;
    }

    [HttpGet]
    [HttpGet("/Expense/GetExpenses")]
    public async Task<ActionResult<List<ExpenseDto>>> GetExpenses([FromQuery] ExpenseFilterRequest filter, CancellationToken ct)
    {
        var result = await _service.GetExpensesAsync(filter, ct);
        return Ok(result);
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
}
