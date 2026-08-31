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

    public async Task<IReadOnlyList<DeliveryCompanyListItem>> ListRepresentativesAsync(
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
            .Select(company => new DeliveryCompanyListItem(
                company.Id,
                company.Name,
                company.ImageUrl))
            .ToArrayAsync(cancellationToken);
    }

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
            .Where(assignment =>
                assignment.ManufacturingCompanyId == storeId.Value)
            .Select(assignment => new
            {
                assignment.DeliveryCompanyId,
                assignment.IsManualTransfer,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return assignment is null || assignment.IsManualTransfer
            ? null
            : assignment.DeliveryCompanyId;
    }

    public async Task<IReadOnlyList<DeliveryOptionListItem>> ListCompaniesAndRepresentativesAsync(
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
            .Select(company => new DeliveryOptionListItem(
                company.Id,
                company.Name,
                company.ImageUrl,
                company.IsRepresentative))
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

    public Task<IReadOnlyList<DeliveryCompanyListItem>> ListRepresentativesAsync(
        IReadOnlyCollection<int>? countryIds,
        IReadOnlyCollection<string>? cityIds,
        CancellationToken cancellationToken) =>
        throw new ReadInfrastructureUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");

    public Task<int?> GetAssignedCompanyIdForOrderAsync(
        int orderId,
        CancellationToken cancellationToken) =>
        throw new ReadInfrastructureUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");

    public Task<IReadOnlyList<DeliveryOptionListItem>> ListCompaniesAndRepresentativesAsync(
        int? countryId,
        string? cityId,
        int? restrictToCompanyId,
        CancellationToken cancellationToken) =>
        throw new ReadInfrastructureUnavailableException(
            "The isolated SQL read infrastructure is not configured in this environment.");
}
