using Microsoft.EntityFrameworkCore;
using Luxira.Api.Data;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Utils.Exceptions;

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
        return await WithRoles(_context.Users.AsNoTracking())
            .FirstOrDefaultAsync(u => u.Id == id, ct);
    }

    public async Task<ApplicationUser?> GetByUsernameAsync(string username, CancellationToken ct = default)
    {
        var normalized = username.Trim().ToUpperInvariant();
        return await WithRoles(_context.Users).FirstOrDefaultAsync(
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
        var requestedRole = user.Role;
        ApplicationRole? role = null;
        if (!string.IsNullOrWhiteSpace(requestedRole))
        {
            var normalizedRole = requestedRole.Trim().ToUpperInvariant();
            role = await _context.Roles.FirstOrDefaultAsync(
                item => item.NormalizedName == normalizedRole,
                ct) ?? throw new BadRequestException(
                    $"Role '{requestedRole}' does not exist.");
        }

        var result = await _context.Users.AddAsync(user, ct);

        if (role is not null)
        {
            var userRole = new ApplicationUserRole
            {
                UserId = user.Id,
                RoleId = role.Id,
                User = user,
                Role = role,
            };
            await _context.UserRoles.AddAsync(userRole, ct);
            user.UserRoles.Add(userRole);
        }

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
        var now = DateTimeOffset.UtcNow;
        return await WithRoles(_context.Users.AsNoTracking())
            .Where(u => !u.LockoutEnd.HasValue || u.LockoutEnd <= now)
            .ToListAsync(ct);
    }

    public async Task<List<UserSwitchGroup>> GetSwitchGroupsForUserAsync(string userId, CancellationToken ct = default)
    {
        return await _context.UserSwitchGroups
            .Include(g => g.Members)
            .Where(g => g.Members.Any(m => m.UserId == userId) || g.CreatedByUserId == userId)
            .AsNoTracking()
            .ToListAsync(ct);
    }

    private static IQueryable<ApplicationUser> WithRoles(
        IQueryable<ApplicationUser> query) =>
        query
            .Include(user => user.UserRoles)
            .ThenInclude(userRole => userRole.Role);
}
