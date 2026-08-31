using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Infrastructure.DeliveryCompanies;

internal sealed class SqlDeliveryCompanyReader(
    IDbContextFactory<LuxiraReadDbContext> contextFactory)
    : IDeliveryCompanyReader
{
    public async Task<IReadOnlyList<DeliveryCompanyListItem>> ListCompaniesAsync(
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
            .Select(company => new DeliveryCompanyListItem(
                company.Id,
                company.Name,
                company.ImageUrl))
            .ToArrayAsync(cancellationToken);
    }
}

internal sealed class UnavailableDeliveryCompanyReader
    : IDeliveryCompanyReader
{
    public Task<IReadOnlyList<DeliveryCompanyListItem>> ListCompaniesAsync(
        IReadOnlyCollection<int>? countryIds,
        CancellationToken cancellationToken) =>
        throw new ReadInfrastructureUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");
}
