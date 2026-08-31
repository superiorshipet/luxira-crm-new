using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryRepresentatives;

namespace Luxira.Infrastructure.Features.DeliveryCompanies.ListDeliveryRepresentatives;

internal sealed class UnavailableListDeliveryRepresentativesRepository
    : IListDeliveryRepresentativesRepository
{
    public Task<IReadOnlyList<DeliveryRepresentativeRecord>> ListAsync(
        IReadOnlyCollection<int>? countryIds,
        IReadOnlyCollection<string>? cityIds,
        CancellationToken cancellationToken) =>
        throw new ReadStoreUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");
}
