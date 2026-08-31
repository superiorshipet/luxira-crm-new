namespace Luxira.Application.Features.DeliveryCompanies.ListDeliveryRepresentatives;

public interface IListDeliveryRepresentativesRepository
{
    Task<IReadOnlyList<DeliveryRepresentativeRecord>> ListAsync(
        IReadOnlyCollection<int>? countryIds,
        IReadOnlyCollection<string>? cityIds,
        CancellationToken cancellationToken);
}
