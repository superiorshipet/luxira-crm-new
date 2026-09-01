using Luxira.Api.Features.Expenses.DTOs;
using Luxira.Api.Features.Expenses.Models;
using Luxira.Api.Features.Expenses.Repositories;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.Expenses.Services;

public class ExpenseService
{
    private readonly ExpenseRepository _repository;

    public ExpenseService(ExpenseRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<ExpenseDto>> GetExpensesAsync(ExpenseFilterRequest filter, CancellationToken ct = default)
    {
        var items = await _repository.GetExpensesAsync(filter, ct);
        return items.Select(e => new ExpenseDto(
            e.Id,
            e.Description,
            e.Amount,
            e.CreatedDate
        )).ToList();
    }

    public async Task<ExpenseDto> CreateExpenseAsync(CreateExpenseRequest request, string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new BadRequestException("Expense description is required.");
        }

        if (request.Amount <= 0)
        {
            throw new BadRequestException("Expense amount must be greater than zero.");
        }

        var expense = new Expense
        {
            Description = request.Description,
            Amount = request.Amount,
            CreatedDate = request.CreatedDate ?? DateTime.UtcNow
        };

        var created = await _repository.AddAsync(expense, ct);
        return new ExpenseDto(
            created.Id,
            created.Description,
            created.Amount,
            created.CreatedDate
        );
    }

    public async Task<List<ExchangeRateDto>> GetExchangeRatesAsync(CancellationToken ct = default)
    {
        var rates = await _repository.GetExchangeRatesAsync(ct);
        return rates.Select(r => new ExchangeRateDto(r.Id, r.Country, r.BuyToUSD, r.SellToUSD)).ToList();
    }

    public async Task UpdateExchangeRateAsync(UpdateExchangeRateRequest request, CancellationToken ct = default)
    {
        if (request.BuyToUSD <= 0 || request.SellToUSD <= 0)
        {
            throw new BadRequestException("Exchange rate must be positive.");
        }

        var rate = new ExchangeRate
        {
            Country = request.Country,
            BuyToUSD = request.BuyToUSD,
            SellToUSD = request.SellToUSD
        };

        await _repository.UpdateExchangeRateAsync(rate, ct);
    }
}
