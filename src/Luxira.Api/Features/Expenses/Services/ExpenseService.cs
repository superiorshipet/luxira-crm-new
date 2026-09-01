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
            e.Country,
            e.Category,
            e.Date,
            e.CreatedByUserId,
            e.AttachmentUrl,
            e.Notes
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
            Country = request.Country,
            Category = request.Category,
            Date = request.Date ?? DateTime.UtcNow,
            CreatedByUserId = userId,
            AttachmentUrl = request.AttachmentUrl,
            Notes = request.Notes
        };

        var created = await _repository.AddAsync(expense, ct);
        return new ExpenseDto(
            created.Id,
            created.Description,
            created.Amount,
            created.Country,
            created.Category,
            created.Date,
            created.CreatedByUserId,
            created.AttachmentUrl,
            created.Notes
        );
    }

    public async Task<List<ExchangeRateDto>> GetExchangeRatesAsync(CancellationToken ct = default)
    {
        var rates = await _repository.GetExchangeRatesAsync(ct);
        return rates.Select(r => new ExchangeRateDto(r.Id, r.FromCurrency, r.ToCurrency, r.Rate, r.UpdatedAt)).ToList();
    }

    public async Task UpdateExchangeRateAsync(UpdateExchangeRateRequest request, CancellationToken ct = default)
    {
        if (request.Rate <= 0)
        {
            throw new BadRequestException("Exchange rate must be positive.");
        }

        var rate = new ExchangeRate
        {
            FromCurrency = request.FromCurrency.ToUpperInvariant(),
            ToCurrency = request.ToCurrency.ToUpperInvariant(),
            Rate = request.Rate,
            UpdatedAt = DateTime.UtcNow
        };

        await _repository.UpdateExchangeRateAsync(rate, ct);
    }
}
