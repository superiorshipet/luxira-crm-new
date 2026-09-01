namespace Luxira.Api.Features.Warehouses.DTOs;

public record WarehouseDto(
    int Id,
    string Name,
    string? DisplayName,
    string Address,
    int Country,
    string? City,
    bool IsActive,
    int? MainWarehouseId,
    string? MainWarehouseName
);

public record CreateWarehouseRequest(
    string Name,
    string? DisplayName,
    string Address,
    int Country,
    string? City,
    int? MainWarehouseId
);

public record MainWarehouseDto(
    int Id,
    string Name,
    int Country,
    bool IsActive,
    IReadOnlyList<WarehouseDto> SubWarehouses
);
