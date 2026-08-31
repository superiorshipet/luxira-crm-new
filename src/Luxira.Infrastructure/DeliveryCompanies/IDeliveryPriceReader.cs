namespace Luxira.Infrastructure.DeliveryCompanies;

public interface IDeliveryPriceReader
{
    Task<decimal> GetPriceAsync(
        int deliveryCompanyId,
        int countryId,
        string? cityId,
        CancellationToken cancellationToken);
}
