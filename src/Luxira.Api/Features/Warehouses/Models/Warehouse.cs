namespace Luxira.Api.Features.Warehouses.Models;

public class Warehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string Address { get; set; } = string.Empty;
    public int Country { get; set; }
    public string? City { get; set; }
    public bool IsActive { get; set; } = true;
    public int? DeliveryCompanyId { get; set; }
    public int? MainWarehouseId { get; set; }
    public MainWarehouse? MainWarehouse { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class MainWarehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int Country { get; set; }
    public bool IsActive { get; set; } = true;
    public List<Warehouse> SubWarehouses { get; set; } = new();
}

public class SubWarehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MainWarehouseId { get; set; }
    public MainWarehouse? MainWarehouse { get; set; }
}

public class ManufacturingCompanyMainWarehouse
{
    public int Id { get; set; }
    public int ManufacturingCompanyId { get; set; }
    public int MainWarehouseId { get; set; }
}

public class WarehouseEditHistory
{
    public int Id { get; set; }
    public int WarehouseId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime EditedAt { get; set; } = DateTime.UtcNow;
    public string? Details { get; set; }
}
