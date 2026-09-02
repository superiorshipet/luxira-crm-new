using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;

namespace Luxira.Api.Features.Auth.Services;

public sealed class AuthCookieService
{
    public async Task SignInTokenAsync(HttpContext context, string token)
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        var identity = new ClaimsIdentity(
            jwt.Claims,
            AuthenticationExtensions.BrowserCookieScheme,
            JwtRegisteredClaimNames.Sub,
            "role");

        await context.SignInAsync(
            AuthenticationExtensions.BrowserCookieScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(3650),
                AllowRefresh = true,
            });
    }

    public Task SignOutAsync(HttpContext context) =>
        context.SignOutAsync(AuthenticationExtensions.BrowserCookieScheme);
}
