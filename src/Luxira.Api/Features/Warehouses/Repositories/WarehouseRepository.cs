using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.Warehouses.Models;

namespace Luxira.Api.Features.Warehouses.Repositories;

public class WarehouseRepository
{
    private readonly ApplicationDbContext _context;

    public WarehouseRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<Warehouse>> GetAllAsync(int? countryId = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.Warehouses
            .Include(w => w.MainWarehouse)
            .AsNoTracking()
            .AsQueryable();

        if (countryId.HasValue && countryId.Value > 0)
        {
            query = query.Where(w => w.Country == countryId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(w => w.IsActive == isActive.Value);
        }

        return await query.OrderBy(w => w.Name).ToListAsync(ct);
    }

    public async Task<Warehouse?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return await _context.Warehouses
            .Include(w => w.MainWarehouse)
            .FirstOrDefaultAsync(w => w.Id == id, ct);
    }

    public async Task<Warehouse> AddAsync(Warehouse warehouse, CancellationToken ct = default)
    {
        var result = await _context.Warehouses.AddAsync(warehouse, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task<List<MainWarehouse>> GetMainWarehousesAsync(int? countryId = null, CancellationToken ct = default)
    {
        var query = _context.MainWarehouses
            .Include(m => m.SubWarehouses)
            .AsNoTracking()
            .AsQueryable();

        if (countryId.HasValue && countryId.Value > 0)
        {
            query = query.Where(m => m.Country == countryId.Value);
        }

        return await query.OrderBy(m => m.Name).ToListAsync(ct);
    }
}
