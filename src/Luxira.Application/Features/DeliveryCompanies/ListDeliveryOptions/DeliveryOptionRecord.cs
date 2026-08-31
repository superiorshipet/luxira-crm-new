namespace Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;

public sealed record DeliveryOptionRecord(
    int Id,
    string Name,
    string? LogoUrl,
    bool IsRepresentative);
