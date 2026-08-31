using Luxira.Application.Features.DeliveryCompanies.ListDeliveryCompanies;
using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Infrastructure.Features.DeliveryCompanies.ListDeliveryCompanies;

internal sealed class SqlListDeliveryCompaniesRepository(
    IDbContextFactory<LuxiraReadDbContext> contextFactory)
    : IListDeliveryCompaniesRepository
{
    public async Task<IReadOnlyList<DeliveryCompanyRecord>> ListAsync(
        IReadOnlyCollection<int>? countryIds,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var query = context.DeliveryCompanies
            .Where(company => company.IsShown && !company.IsRepresentative);

        if (countryIds is { Count: > 0 })
        {
            query = query.Where(company => countryIds.Contains(company.Country));
        }

        return await query
            .Select(company => new DeliveryCompanyRecord(
                company.Id,
                company.Name,
                company.ImageUrl))
            .ToArrayAsync(cancellationToken);
    }
}
