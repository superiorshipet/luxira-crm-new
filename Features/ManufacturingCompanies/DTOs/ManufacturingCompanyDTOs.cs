namespace Luxira.Api.Features.ManufacturingCompanies.DTOs;

public record ManufacturingCompanyDto(
    int Id,
    string Name,
    string? ImageUrl,
    bool IsShown,
    string? InvoiceImage,
    string? ImageUrl2,
    string? PhoneNumber,
    int? MainWarehouseId,
    bool IsPasswordEmailStore
);

public record CreateManufacturingCompanyRequest(
    string Name,
    string? PhoneNumber = null,
    int? MainWarehouseId = null,
    bool IsShown = true,
    string? ImageUrl = null,
    string? ImageUrl2 = null,
    string? InvoiceImage = null
);

public record ProductDto(
    int Id,
    string Name,
    int Country,
    decimal Price,
    decimal MinimumSellingPrice,
    decimal MaximumSellingPrice,
    decimal DeliveryPrice,
    int Quantity,
    string SaleType,
    string? ImageUrl,
    int ManufacturingCompanyId,
    string? ManufacturingCompanyName,
    bool IsDeleted
);

public record CreateProductRequest(
    string Name,
    int Country,
    decimal MinimumSellingPrice,
    decimal MaximumSellingPrice,
    decimal DeliveryPrice,
    int Quantity,
    string? SaleType,
    int ManufacturingCompanyId,
    string? ImageUrl = null
);

public record ProductMinimumPriceDto(
    int Id,
    int Country,
    int ManufacturingCompanyId,
    int MainWarehouseId,
    decimal MinimumSellingPrice,
    DateTime CreatedAt,
    DateTime? UpdatedAt
);

public record SetProductMinimumPriceRequest(
    int Country,
    int ManufacturingCompanyId,
    int MainWarehouseId,
    decimal MinimumSellingPrice
);
