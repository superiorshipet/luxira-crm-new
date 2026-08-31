using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryCompanies;

namespace Luxira.Infrastructure.Features.DeliveryCompanies.ListDeliveryCompanies;

internal sealed class UnavailableListDeliveryCompaniesRepository
    : IListDeliveryCompaniesRepository
{
    public Task<IReadOnlyList<DeliveryCompanyRecord>> ListAsync(
        IReadOnlyCollection<int>? countryIds,
        CancellationToken cancellationToken) =>
        throw new ReadStoreUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");
}
