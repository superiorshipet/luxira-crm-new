using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.ManufacturingCompanies.Models;

namespace Luxira.Api.Features.ManufacturingCompanies.Repositories;

public class ManufacturingCompanyRepository
{
    private readonly ApplicationDbContext _context;

    public ManufacturingCompanyRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ManufacturingCompany>> GetAllCompaniesAsync(int? countryId = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.ManufacturingCompanies.AsNoTracking().AsQueryable();

        if (countryId.HasValue && countryId.Value > 0)
        {
            query = query.Where(m => m.Country == countryId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(m => m.IsActive == isActive.Value);
        }

        return await query.OrderBy(m => m.Name).ToListAsync(ct);
    }

    public async Task<ManufacturingCompany?> GetCompanyByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.ManufacturingCompanies
            .Include(m => m.Products)
            .ThenInclude(p => p.Images)
            .FirstOrDefaultAsync(m => m.Id == id, ct);
    }

    public async Task<ManufacturingCompany> AddCompanyAsync(ManufacturingCompany company, CancellationToken ct = default)
    {
        var result = await _context.ManufacturingCompanies.AddAsync(company, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<List<MainProduct>> GetProductsAsync(int? companyId = null, CancellationToken ct = default)
    {
        var query = _context.MainProducts
            .Include(p => p.ManufacturingCompany)
            .Include(p => p.Images)
            .AsNoTracking()
            .AsQueryable();

        if (companyId.HasValue && companyId.Value > 0)
        {
            query = query.Where(p => p.ManufacturingCompanyId == companyId.Value);
        }

        return await query.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<MainProduct?> GetProductByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.MainProducts
            .Include(p => p.ManufacturingCompany)
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<MainProduct> AddProductAsync(MainProduct product, CancellationToken ct = default)
    {
        var result = await _context.MainProducts.AddAsync(product, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<List<ProductMinimumSellingPrice>> GetMinimumPricesAsync(int? productId = null, int? countryId = null, CancellationToken ct = default)
    {
        var query = _context.ProductMinimumSellingPrices.AsNoTracking().AsQueryable();

        if (productId.HasValue && productId.Value > 0)
        {
            query = query.Where(p => p.MainProductId == productId.Value);
        }

        if (countryId.HasValue && countryId.Value > 0)
        {
            query = query.Where(p => p.Country == countryId.Value);
        }

        return await query.ToListAsync(ct);
    }

    public async Task SetMinimumPriceAsync(ProductMinimumSellingPrice minPrice, CancellationToken ct = default)
    {
        var existing = await _context.ProductMinimumSellingPrices
            .FirstOrDefaultAsync(p => p.MainProductId == minPrice.MainProductId && p.Country == minPrice.Country, ct);

        if (existing != null)
        {
            existing.MinimumPrice = minPrice.MinimumPrice;
        }
        else
        {
            await _context.ProductMinimumSellingPrices.AddAsync(minPrice, ct);
        }

        await _context.SaveChangesAsync(ct);
    }
}
