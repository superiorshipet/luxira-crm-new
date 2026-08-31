namespace Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;

public interface IListDeliveryOptionsRepository
{
    Task<int?> GetAssignedCompanyIdForOrderAsync(
        int orderId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DeliveryOptionRecord>> ListAsync(
        int? countryId,
        string? cityId,
        int? restrictToCompanyId,
        CancellationToken cancellationToken);
}
