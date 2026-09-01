using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.Auth.Models;

namespace Luxira.Api.Features.Auth.Repositories;

public class UserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<ApplicationUser?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        return await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToUpperInvariant();
        return await _context.Users.FirstOrDefaultAsync(
            u => u.NormalizedUserName == normalized ||
                 u.NormalizedEmail == normalized,
            ct);
    }

    public async Task<ApplicationUser?> FindByNormalizedIdentityAsync(
        string normalizedUsername,
        string normalizedEmail,
        CancellationToken ct = default)
    {
        return await _context.Users.FirstOrDefaultAsync(
            u => u.NormalizedUserName == normalizedUsername ||
                 u.NormalizedEmail == normalizedEmail,
            ct);
    }

    public async Task<ApplicationUser> AddAsync(ApplicationUser user, CancellationToken ct = default)
    {
        var result = await _context.Users.AddAsync(user, ct);
        await _context.SaveChangesAsync(ct);
        return result.Entity;
    }

    public async Task UpdateAsync(ApplicationUser user, CancellationToken ct = default)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<List<ApplicationUser>> GetAllActiveAsync(CancellationToken ct = default)
    {
        return await _context.Users.AsNoTracking().Where(u => u.IsActive).ToListAsync(ct);
    }

    public async Task<List<UserSwitchGroup>> GetSwitchGroupsForUserAsync(string userId, CancellationToken ct = default)
    {
        return await _context.UserSwitchGroups
            .Include(g => g.Members)
            .Where(g => g.Members.Any(m => m.UserId == userId) || g.CreatedByUserId == userId)
            .AsNoTracking()
            .ToListAsync(ct);
    }
}
