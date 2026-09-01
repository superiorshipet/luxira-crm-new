using Luxira.Api.Features.Warehouses.DTOs;
using Luxira.Api.Features.Warehouses.Models;
using Luxira.Api.Features.Warehouses.Repositories;
using Luxira.Api.Utils.Exceptions;

namespace Luxira.Api.Features.Warehouses.Services;

public class WarehouseService
{
    private readonly WarehouseRepository _repository;

    public WarehouseService(WarehouseRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<WarehouseDto>> GetWarehousesAsync(int? countryId = null, bool? isActive = null, CancellationToken ct = default)
    {
        var items = await _repository.GetAllAsync(countryId, isActive, ct);
        return items.Select(MapToDto).ToList();
    }

    public async Task<WarehouseDto> GetWarehouseByIdAsync(int id, CancellationToken ct = default)
    {
        var warehouse = await _repository.GetByIdAsync(id, ct);
        if (warehouse == null)
        {
            throw new NotFoundException($"Warehouse with ID {id} not found.");
        }
        return MapToDto(warehouse);
    }

    public async Task<WarehouseDto> CreateWarehouseAsync(CreateWarehouseRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new BadRequestException("Warehouse name is required.");
        }

        var entity = new Warehouse
        {
            Name = request.Name,
            DisplayName = request.DisplayName,
            Address = request.Address,
            Country = request.Country,
            City = request.City,
            MainWarehouseId = request.MainWarehouseId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        var created = await _repository.AddAsync(entity, ct);
        return MapToDto(created);
    }

    public async Task<List<MainWarehouseDto>> GetMainWarehousesAsync(int? countryId = null, CancellationToken ct = default)
    {
        var main = await _repository.GetMainWarehousesAsync(countryId, ct);
        return main.Select(m => new MainWarehouseDto(
            m.Id,
            m.Name,
            m.Country,
            m.IsActive,
            m.SubWarehouses.Select(MapToDto).ToList()
        )).ToList();
    }

    private static WarehouseDto MapToDto(Warehouse w) => new(
        w.Id,
        w.Name ?? string.Empty,
        w.DisplayName,
        w.Address,
        w.Country,
        w.City,
        w.IsActive,
        w.MainWarehouseId,
        w.MainWarehouse?.Name
    );
}
