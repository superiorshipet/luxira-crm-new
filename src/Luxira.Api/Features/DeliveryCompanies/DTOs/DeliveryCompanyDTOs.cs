namespace Luxira.Api.Features.DeliveryCompanies.DTOs;

public record DeliveryCompanyRecord(
    int Id,
    string Name,
    string? DisplayName,
    string? ImageUrl,
    string? InformationUrl,
    string? TaxRegistrationNumber,
    string Address,
    string IdNumber,
    string PhoneNumber,
    string? Specialty,
    string? Website,
    string? Notes,
    int Country,
    string? City,
    DateTime CreatedDate,
    bool IsActive,
    bool IsShown,
    bool IsRepresentative,
    bool AutoConvertDeliveredToBalanceUpdated
);

public record DeliveryCompanyResult(
    IReadOnlyList<DeliveryCompanyRecord> Items,
    int TotalCount
);

public record DeliveryPriceResult(
    int DeliveryCompanyId,
    int CountryId,
    string? CityId,
    decimal Price,
    string Source
);

public record DeliveryOptionRecord(
    int Id,
    string Name,
    string? DisplayName,
    int CountryId,
    string? City,
    bool IsRepresentative
);

public record DeliveryOptionResult(
    IReadOnlyList<DeliveryOptionRecord> Items,
    int TotalCount
);

public record DeliveryRepresentativeRecord(
    int Id,
    string Name,
    string? DisplayName,
    string PhoneNumber,
    string Address,
    int Country,
    string? City,
    bool IsActive
);

public record DeliveryRepresentativeResult(
    IReadOnlyList<DeliveryRepresentativeRecord> Items,
    int TotalCount
);

public record CreateDeliveryCompanyRequest(
    string Name,
    string? DisplayName,
    string Address,
    string IdNumber,
    string PhoneNumber,
    int Country,
    string? City,
    string? Specialty,
    string? Website,
    string? Notes,
    bool IsRepresentative
);

public record SetDeliveryPriceRequest(
    int DeliveryCompanyId,
    int Country,
    string? City,
    decimal Price
);
