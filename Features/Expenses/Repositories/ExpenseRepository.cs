using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.Expenses.DTOs;
using Luxira.Api.Features.Expenses.Models;

namespace Luxira.Api.Features.Expenses.Repositories;

public class ExpenseRepository
{
    private readonly ApplicationDbContext _context;

    public ExpenseRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Expense>> GetExpensesAsync(ExpenseFilterRequest filter, CancellationToken ct = default)
    {
        var query = _context.Expenses.AsNoTracking().AsQueryable();

        if (filter.Country.HasValue && filter.Country.Value > 0)
        {
            query = query.Where(e => e.Country == filter.Country.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            query = query.Where(e => e.Category == filter.Category);
        }

        if (filter.FromDate.HasValue)
        {
            query = query.Where(e => e.Date >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(e => e.Date <= filter.ToDate.Value);
        }

        return await query.OrderByDescending(e => e.Date).ToListAsync(ct);
    }

    public async Task<Expense> AddAsync(Expense expense, CancellationToken ct = default)
    {
        var result = await _context.Expenses.AddAsync(expense, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<List<ExchangeRate>> GetExchangeRatesAsync(CancellationToken ct = default)
    {
        return await _context.ExchangeRates.AsNoTracking().ToListAsync(ct);
    }

    public async Task UpdateExchangeRateAsync(ExchangeRate rate, CancellationToken ct = default)
    {
        var existing = await _context.ExchangeRates.FirstOrDefaultAsync(r => r.FromCurrency == rate.FromCurrency && r.ToCurrency == rate.ToCurrency, ct);
        if (existing != null)
        {
            existing.Rate = rate.Rate;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            await _context.ExchangeRates.AddAsync(rate, ct);
        }
        await _context.SaveChangesAsync(ct);
    }
}
