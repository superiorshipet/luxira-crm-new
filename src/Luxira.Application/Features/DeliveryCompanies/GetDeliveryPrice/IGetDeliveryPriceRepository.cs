namespace Luxira.Application.Features.DeliveryCompanies.GetDeliveryPrice;

public interface IGetDeliveryPriceRepository
{
    Task<decimal> GetAsync(
        int deliveryCompanyId,
        int countryId,
        string? cityId,
        CancellationToken cancellationToken);
}
