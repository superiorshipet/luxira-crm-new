namespace Luxira.Application.Features.DeliveryCompanies.ListDeliveryCompanies;

public interface IListDeliveryCompaniesRepository
{
    Task<IReadOnlyList<DeliveryCompanyRecord>> ListAsync(
        IReadOnlyCollection<int>? countryIds,
        CancellationToken cancellationToken);
}
