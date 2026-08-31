using Luxira.Application.Abstractions.Persistence;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;

namespace Luxira.Infrastructure.Features.DeliveryCompanies.ListDeliveryOptions;

internal sealed class UnavailableListDeliveryOptionsRepository
    : IListDeliveryOptionsRepository
{
    public Task<int?> GetAssignedCompanyIdForOrderAsync(
        int orderId,
        CancellationToken cancellationToken) =>
        throw CreateException();

    public Task<IReadOnlyList<DeliveryOptionRecord>> ListAsync(
        int? countryId,
        string? cityId,
        int? restrictToCompanyId,
        CancellationToken cancellationToken) =>
        throw CreateException();

    private static ReadStoreUnavailableException CreateException() =>
        new("The isolated SQL read infrastructure is not configured in this environment.");
}
