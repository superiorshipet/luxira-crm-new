using Luxira.Application.Features.DeliveryCompanies.ListDeliveryRepresentatives;
using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Infrastructure.Features.DeliveryCompanies.ListDeliveryRepresentatives;

internal sealed class SqlListDeliveryRepresentativesRepository(
    IDbContextFactory<LuxiraReadDbContext> contextFactory)
    : IListDeliveryRepresentativesRepository
{
    public async Task<IReadOnlyList<DeliveryRepresentativeRecord>> ListAsync(
        IReadOnlyCollection<int>? countryIds,
        IReadOnlyCollection<string>? cityIds,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var query = context.DeliveryCompanies
            .Where(company => company.IsShown && company.IsRepresentative);

        if (countryIds is { Count: > 0 })
        {
            query = query.Where(company => countryIds.Contains(company.Country));
        }

        if (cityIds is not null && cityIds.Any(city =>
                !string.IsNullOrWhiteSpace(city)))
        {
            query = query.Where(company => cityIds.Contains(company.City!));
        }

        return await query
            .Select(company => new DeliveryRepresentativeRecord(
                company.Id,
                company.Name,
                company.ImageUrl))
            .ToArrayAsync(cancellationToken);
    }
}
