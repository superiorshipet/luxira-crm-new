using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Infrastructure.DeliveryCompanies;

internal sealed class SqlDeliveryPriceReader(
    IDbContextFactory<LuxiraReadDbContext> contextFactory)
    : IDeliveryPriceReader
{
    public async Task<decimal> GetPriceAsync(
        int deliveryCompanyId,
        int countryId,
        string? cityId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);

        return await context.DeliveryCompanyPrices
            .Where(price =>
                price.DeliveryCompanyId == deliveryCompanyId &&
                price.Country == countryId &&
                (price.City == null || price.City == cityId || cityId == null))
            .OrderByDescending(price => price.City == cityId)
            .Select(price => (decimal?)price.Price)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;
    }
}

internal sealed class UnavailableDeliveryPriceReader : IDeliveryPriceReader
{
    public Task<decimal> GetPriceAsync(
        int deliveryCompanyId,
        int countryId,
        string? cityId,
        CancellationToken cancellationToken) =>
        throw new ReadInfrastructureUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");
}
