namespace Luxira.Application.Features.DeliveryCompanies.GetDeliveryPrice;

public sealed class GetDeliveryPriceService(IGetDeliveryPriceRepository repository)
{
    public async Task<DeliveryPriceResult> ExecuteAsync(
        int deliveryCompanyId,
        int countryId,
        string? cityId,
        CancellationToken cancellationToken)
    {
        var price = await repository.GetAsync(
            deliveryCompanyId,
            countryId,
            cityId,
            cancellationToken);
        return new DeliveryPriceResult(price);
    }
}
