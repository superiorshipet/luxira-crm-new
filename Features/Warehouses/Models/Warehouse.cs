namespace Luxira.Api.Features.Warehouses.Models;

public class Warehouse
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public decimal Price { get; set; }
    public int Amount { get; set; }
    public int UnchangingAmount { get; set; }
    public int ReservedAmount { get; set; }
    public int DeliveryCompanyId { get; set; }
    public int? ManufacturingCompanyId { get; set; }
    public DateTime DateAdded { get; set; }
    public DateTime DateUpdated { get; set; }
    public int Countries { get; set; }
    public string? City { get; set; }
    public bool IsShown { get; set; } = true;
    public int? MainWarehouseId { get; set; }
    public MainWarehouse? MainWarehouse { get; set; }
    public int? SubWarehouseId { get; set; }

    // API compatibility aliases backed by the legacy database columns.
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string? DisplayName { get => Name; set => Name = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public string Address { get => string.Empty; set { } }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int Country { get => Countries; set => Countries = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsActive { get => IsShown; set => IsShown = value; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime CreatedAt { get => DateAdded; set => DateAdded = value; }
}

public class MainWarehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string? ImageS3Key { get; set; }
    public List<Warehouse> SubWarehouses { get; set; } = new();

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public int Country => 0;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsActive => true;
}

public class SubWarehouse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? MainWarehouseId { get; set; }
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
    public DateTime EditDate { get; set; }
    public int AddedAmount { get; set; }
    public string ApplicationUserId { get; set; } = string.Empty;
}
