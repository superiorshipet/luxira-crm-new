using Luxira.Api.Data;
using Luxira.Api.Features.Communication.Models;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Communication.Repositories;

public sealed class PasswordEmailRepository(ApplicationDbContext context)
{
    public Task<List<PasswordEmail>> ListAsync(
        bool deleted,
        string? email,
        int? storeId,
        CancellationToken ct)
    {
        var query = context.PasswordEmails
            .AsNoTracking()
            .Include(item => item.ManufacturingCompany)
            .Where(item => item.IsDeleted == deleted);

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(item => item.Email.Contains(email.Trim()));
        if (storeId is > 0)
            query = query.Where(item => item.ManufacturingCompanyId == storeId);

        return query
            .OrderByDescending(item => deleted ? item.DeletedAt : item.UpdatedAt ?? item.CreatedAt)
            .Take(500)
            .ToListAsync(ct);
    }

    public Task<PasswordEmail?> GetAsync(int id, bool? deleted, CancellationToken ct)
    {
        var query = context.PasswordEmails
            .Include(item => item.ManufacturingCompany)
            .Where(item => item.Id == id);
        if (deleted.HasValue) query = query.Where(item => item.IsDeleted == deleted.Value);
        return query.FirstOrDefaultAsync(ct);
    }

    public Task<bool> StoreExistsAsync(int id, CancellationToken ct) =>
        context.ManufacturingCompanies.AsNoTracking().AnyAsync(store => store.Id == id, ct);

    public Task<bool> EmailExistsAsync(string email, int? excludedId, CancellationToken ct) =>
        context.PasswordEmails.AsNoTracking().AnyAsync(
            item => !item.IsDeleted && item.Email == email && item.Id != excludedId,
            ct);

    public async Task AddAsync(PasswordEmail item, CancellationToken ct)
    {
        await context.PasswordEmails.AddAsync(item, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task SaveAsync(PasswordEmailHistory history, CancellationToken ct)
    {
        await context.PasswordEmailHistories.AddAsync(history, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task PermanentlyDeleteAsync(PasswordEmail item, CancellationToken ct)
    {
        context.PasswordEmails.Remove(item);
        await context.SaveChangesAsync(ct);
    }

    public Task<List<PasswordEmailHistory>> ListHistoryAsync(int? itemId, CancellationToken ct)
    {
        var query = context.PasswordEmailHistories.AsNoTracking();
        if (itemId is > 0) query = query.Where(history => history.PasswordEmailId == itemId);
        return query.OrderByDescending(history => history.ChangedAt).Take(150).ToListAsync(ct);
    }
}
