using Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;
using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Infrastructure.Features.DeliveryCompanies.ListDeliveryOptions;

internal sealed class SqlListDeliveryOptionsRepository(
    IDbContextFactory<LuxiraReadDbContext> contextFactory)
    : IListDeliveryOptionsRepository
{
    public async Task<int?> GetAssignedCompanyIdForOrderAsync(
        int orderId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var storeId = await context.Orders
            .Where(order => order.Id == orderId)
            .Select(order => order.ManufacturingCompanyId)
            .FirstOrDefaultAsync(cancellationToken);
        if (!storeId.HasValue)
        {
            return null;
        }

        var assignment = await context.StoreDeliveryAssignments
            .Where(candidate =>
                candidate.ManufacturingCompanyId == storeId.Value)
            .Select(candidate => new
            {
                candidate.DeliveryCompanyId,
                candidate.IsManualTransfer,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return assignment is null || assignment.IsManualTransfer
            ? null
            : assignment.DeliveryCompanyId;
    }

    public async Task<IReadOnlyList<DeliveryOptionRecord>> ListAsync(
        int? countryId,
        string? cityId,
        int? restrictToCompanyId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var query = context.DeliveryCompanies.Where(company => company.IsShown);

        if (restrictToCompanyId.HasValue)
        {
            query = query.Where(company => company.Id == restrictToCompanyId.Value);
        }

        if (countryId.HasValue)
        {
            query = query.Where(company => company.Country == countryId.Value);
        }

        var companies = query.Where(company => !company.IsRepresentative);
        var representatives = query.Where(company => company.IsRepresentative);
        if (!string.IsNullOrEmpty(cityId))
        {
            representatives = representatives.Where(company => company.City == cityId);
        }

        return await companies
            .Concat(representatives)
            .Select(company => new DeliveryOptionRecord(
                company.Id,
                company.Name,
                company.ImageUrl,
                company.IsRepresentative))
            .ToArrayAsync(cancellationToken);
    }
}
