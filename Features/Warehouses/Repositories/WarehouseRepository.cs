using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.Warehouses.DTOs;
using Luxira.Api.Features.Warehouses.Models;

namespace Luxira.Api.Features.Warehouses.Repositories;

public class WarehouseRepository
{
    private readonly ApplicationDbContext _context;

    public WarehouseRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<WarehouseDto>> GetAllAsync(int? countryId = null, bool? isActive = null, CancellationToken ct = default)
    {
        var query = _context.Warehouses
            .AsNoTracking()
            .AsQueryable();

        if (countryId.HasValue && countryId.Value > 0)
        {
            query = query.Where(w => w.Countries == countryId.Value);
        }

        if (isActive.HasValue)
        {
            query = query.Where(w => w.IsShown == isActive.Value);
        }

        return await query.OrderBy(w => w.Name)
            .Select(w => new WarehouseDto(
                w.Id,
                w.Name ?? string.Empty,
                w.Name,
                string.Empty,
                w.Countries,
                w.City,
                w.IsShown,
                w.MainWarehouseId,
                w.MainWarehouse != null ? w.MainWarehouse.Name : null))
            .ToListAsync(ct);
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

    public async Task<List<MainWarehouseDto>> GetMainWarehousesAsync(int? countryId = null, CancellationToken ct = default)
    {
        var query = _context.MainWarehouses
            .AsNoTracking()
            .AsQueryable();

        if (countryId.HasValue && countryId.Value > 0)
        {
            query = query.Where(m => m.SubWarehouses.Any(w => w.Countries == countryId.Value));
        }

        return await query.OrderBy(m => m.Name)
            .Select(main => new MainWarehouseDto(
                main.Id,
                main.Name,
                0,
                true,
                main.SubWarehouses.OrderBy(warehouse => warehouse.Name)
                    .Select(warehouse => new WarehouseDto(
                        warehouse.Id,
                        warehouse.Name ?? string.Empty,
                        warehouse.Name,
                        string.Empty,
                        warehouse.Countries,
                        warehouse.City,
                        warehouse.IsShown,
                        warehouse.MainWarehouseId,
                        main.Name))
                    .ToList()))
            .ToListAsync(ct);
    }
}
