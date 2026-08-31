namespace Luxira.Application.Features.DeliveryCompanies.ListDeliveryCompanies;

public sealed class ListDeliveryCompaniesService(
    IListDeliveryCompaniesRepository repository)
{
    public async Task<IReadOnlyList<DeliveryCompanyResult>> ExecuteAsync(
        IReadOnlyCollection<int>? countryIds,
        CancellationToken cancellationToken)
    {
        var companies = await repository.ListAsync(countryIds, cancellationToken);
        return companies
            .Select(company => new DeliveryCompanyResult(
                company.Id,
                company.Name,
                DeliveryMediaUrl.Resolve(company.LogoUrl)))
            .ToArray();
    }
}
