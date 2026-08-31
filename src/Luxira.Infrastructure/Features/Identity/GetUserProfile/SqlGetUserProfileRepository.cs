using Luxira.Application.Features.Identity.GetUserProfile;
using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Infrastructure.Features.Identity.GetUserProfile;

internal sealed class SqlGetUserProfileRepository(
    IDbContextFactory<LuxiraReadDbContext> contextFactory)
    : IGetUserProfileRepository
{
    public async Task<UserProfileSource?> GetAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(
            cancellationToken);
        var user = await context.ProfileUsers
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new
            {
                candidate.Id,
                candidate.UserName,
                candidate.Email,
                candidate.DisplayName,
                candidate.PhoneNumber,
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return null;
        }

        var employee = await context.EmployeeProfiles
            .Where(candidate => candidate.ApplicationUserId == userId)
            .OrderByDescending(candidate => candidate.IsActive)
            .ThenByDescending(candidate => candidate.Id)
            .Select(candidate => new
            {
                candidate.DisplayName,
                candidate.Name,
                candidate.ImageUrl,
                candidate.JobTitle,
                candidate.PhoneNumber,
            })
            .FirstOrDefaultAsync(cancellationToken);

        string? firstRole = null;
        if (employee is null || string.IsNullOrWhiteSpace(employee.JobTitle))
        {
            firstRole = await (
                    from userRole in context.UserRoles
                    join role in context.Roles on userRole.RoleId equals role.Id
                    where userRole.UserId == userId
                    select role.Name)
                .FirstOrDefaultAsync(cancellationToken);
        }

        return new UserProfileSource(
            user.Id,
            user.UserName,
            user.Email,
            user.DisplayName,
            user.PhoneNumber,
            employee?.DisplayName,
            employee?.Name,
            employee?.ImageUrl,
            employee?.JobTitle,
            employee?.PhoneNumber,
            firstRole);
    }
}
