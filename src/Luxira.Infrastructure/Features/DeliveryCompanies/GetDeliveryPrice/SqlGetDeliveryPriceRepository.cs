using Luxira.Application.Features.DeliveryCompanies.GetDeliveryPrice;
using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Infrastructure.Features.DeliveryCompanies.GetDeliveryPrice;

internal sealed class SqlGetDeliveryPriceRepository(
    IDbContextFactory<LuxiraReadDbContext> contextFactory)
    : IGetDeliveryPriceRepository
{
    public async Task<decimal> GetAsync(
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
