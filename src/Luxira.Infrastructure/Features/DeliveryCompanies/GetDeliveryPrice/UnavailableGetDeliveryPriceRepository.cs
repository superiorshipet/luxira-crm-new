using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.DeliveryCompanies.GetDeliveryPrice;

namespace Luxira.Infrastructure.Features.DeliveryCompanies.GetDeliveryPrice;

internal sealed class UnavailableGetDeliveryPriceRepository
    : IGetDeliveryPriceRepository
{
    public Task<decimal> GetAsync(
        int deliveryCompanyId,
        int countryId,
        string? cityId,
        CancellationToken cancellationToken) =>
        throw new ReadStoreUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");
}
