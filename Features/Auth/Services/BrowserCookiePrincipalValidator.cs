using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Luxira.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace Luxira.Api.Features.Auth.Services;

public static class BrowserCookiePrincipalValidator
{
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var now = DateTimeOffset.UtcNow;
        if (context.Properties.IssuedUtc is { } issued && now - issued < TimeSpan.FromMinutes(5))
        {
            return;
        }

        var principal = context.Principal;
        var userId = principal?.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (principal is null || string.IsNullOrWhiteSpace(userId))
        {
            await RejectAsync(context);
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users
            .AsNoTracking()
            .Where(item => item.Id == userId)
            .Select(item => new { item.SecurityStamp, item.LockoutEnd })
            .FirstOrDefaultAsync(context.HttpContext.RequestAborted);
        if (user is null ||
            !string.Equals(
                user.SecurityStamp ?? string.Empty,
                principal.FindFirstValue(LuxiraClaimTypes.SecurityStamp) ?? string.Empty,
                StringComparison.Ordinal) ||
            IsBlocked(user.LockoutEnd, principal))
        {
            await RejectAsync(context);
            return;
        }

        var databaseRoles = await (
                from userRole in db.UserRoles.AsNoTracking()
                join role in db.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == userId && role.Name != null
                select role.Name!)
            .ToListAsync(context.HttpContext.RequestAborted);
        var principalRoles = principal.FindAll("role").Select(claim => claim.Value);
        if (!CanonicalRoles(databaseRoles).SetEquals(CanonicalRoles(principalRoles)))
        {
            await RejectAsync(context);
            return;
        }

        var employeeInactive = await db.Employees.AsNoTracking().AnyAsync(
            employee => employee.ApplicationUserId == userId && !employee.IsActive,
            context.HttpContext.RequestAborted);
        if (employeeInactive && !IsPrivilegedSwitch(principal))
        {
            await RejectAsync(context);
            return;
        }

        context.Properties.IssuedUtc = now;
        context.ShouldRenew = true;
    }

    private static bool IsBlocked(DateTimeOffset? lockoutEnd, ClaimsPrincipal principal) =>
        lockoutEnd > DateTimeOffset.UtcNow && !IsPrivilegedSwitch(principal);

    private static bool IsPrivilegedSwitch(ClaimsPrincipal principal) =>
        string.Equals(
            principal.FindFirstValue(LuxiraClaimTypes.AdminSwitchSession),
            "true",
            StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> CanonicalRoles(IEnumerable<string> roles) =>
        roles.Select(role => role.Equals("Administrator", StringComparison.OrdinalIgnoreCase) ? "Admin" : role)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(AuthenticationExtensions.BrowserCookieScheme);
    }
}
