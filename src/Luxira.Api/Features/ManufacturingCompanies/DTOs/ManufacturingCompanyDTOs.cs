namespace Luxira.Api.Features.ManufacturingCompanies.DTOs;

public record ManufacturingCompanyDto(
    int Id,
    string Name,
    string? DisplayName,
    string? Code,
    int Country,
    string? Notes,
    bool IsActive
);

public record CreateManufacturingCompanyRequest(
    string Name,
    string? DisplayName,
    string? Code,
    int Country,
    string? Notes
);

public record ProductDto(
    int Id,
    string Name,
    string? SKU,
    decimal DefaultPrice,
    decimal? DefaultCost,
    int ManufacturingCompanyId,
    string? ManufacturingCompanyName,
    bool IsActive,
    IReadOnlyList<string> ImageUrls
);

public record CreateProductRequest(
    string Name,
    string? SKU,
    decimal DefaultPrice,
    decimal? DefaultCost,
    int ManufacturingCompanyId
);

public record ProductMinimumPriceDto(
    int Id,
    int MainProductId,
    string ProductName,
    int Country,
    decimal MinimumPrice
);

public record SetProductMinimumPriceRequest(
    int MainProductId,
    int Country,
    decimal MinimumPrice
);
