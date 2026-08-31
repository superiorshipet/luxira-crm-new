namespace Luxira.Application.Features.DeliveryCompanies.ListDeliveryRepresentatives;

public sealed class ListDeliveryRepresentativesService(
    IListDeliveryRepresentativesRepository repository)
{
    public async Task<IReadOnlyList<DeliveryRepresentativeResult>> ExecuteAsync(
        IReadOnlyCollection<int>? countryIds,
        IReadOnlyCollection<string>? cityIds,
        CancellationToken cancellationToken)
    {
        var representatives = await repository.ListAsync(
            countryIds,
            cityIds,
            cancellationToken);
        return representatives
            .Select(representative => new DeliveryRepresentativeResult(
                representative.Id,
                representative.Name,
                DeliveryMediaUrl.Resolve(representative.LogoUrl)))
            .ToArray();
    }
}
