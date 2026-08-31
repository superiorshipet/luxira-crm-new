namespace Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;

public sealed record DeliveryOptionResult(
    int Id,
    string Name,
    string LogoUrl,
    bool IsRepresentative);
