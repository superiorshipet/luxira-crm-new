using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.DeliveryCompanies.Models;

namespace Luxira.Api.Features.DeliveryCompanies.Repositories;

public class DeliveryCompanyRepository
{
    private readonly ApplicationDbContext _context;

    public DeliveryCompanyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<DeliveryCompany>> GetAllAsync(int? countryId = null, bool? isRepresentative = null, CancellationToken ct = default)
    {
        var query = _context.DeliveryCompanies.AsNoTracking().AsQueryable();

        if (countryId.HasValue && countryId.Value > 0)
        {
            query = query.Where(d => d.Country == countryId.Value);
        }

        if (isRepresentative.HasValue)
        {
            query = query.Where(d => d.IsRepresentative == isRepresentative.Value);
        }

        return await query.OrderBy(d => d.Name).ToListAsync(ct);
    }

    public async Task<DeliveryCompany?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.DeliveryCompanies
            .Include(d => d.Prices)
            .FirstOrDefaultAsync(d => d.Id == id, ct);
    }

    public async Task<decimal?> GetSpecificDeliveryPriceAsync(int deliveryCompanyId, int countryId, string? city, CancellationToken ct = default)
    {
        var prices = await _context.DeliveryCompanyPrices
            .AsNoTracking()
            .Where(p => p.DeliveryCompanyId == deliveryCompanyId && p.Country == countryId)
            .ToListAsync(ct);

        if (!string.IsNullOrEmpty(city))
        {
            var cityPrice = prices.FirstOrDefault(p => !string.IsNullOrEmpty(p.City) && string.Equals(p.City.Trim(), city.Trim(), StringComparison.OrdinalIgnoreCase));
            if (cityPrice != null)
            {
                return cityPrice.Price;
            }
        }

        var defaultCountryPrice = prices.FirstOrDefault(p => string.IsNullOrEmpty(p.City));
        return defaultCountryPrice?.Price;
    }

    public async Task<DeliveryCompany> AddAsync(DeliveryCompany company, CancellationToken ct = default)
    {
        var result = await _context.DeliveryCompanies.AddAsync(company, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task UpdateAsync(DeliveryCompany company, CancellationToken ct = default)
    {
        _context.DeliveryCompanies.Update(company);
        await _context.SaveChangesAsync(ct);
    }

    public async Task SetPriceAsync(DeliveryCompanyPrice price, CancellationToken ct = default)
    {
        var existing = await _context.DeliveryCompanyPrices
            .FirstOrDefaultAsync(p => p.DeliveryCompanyId == price.DeliveryCompanyId 
                                   && p.Country == price.Country 
                                   && p.City == price.City, ct);

        if (existing != null)
        {
            existing.Price = price.Price;
            _context.DeliveryCompanyPrices.Update(existing);
        }
        else
        {
            await _context.DeliveryCompanyPrices.AddAsync(price, ct);
        }

        await _context.SaveChangesAsync(ct);
    }
}
