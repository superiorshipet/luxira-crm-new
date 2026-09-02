using Luxira.Api.Core;
using Luxira.Api.Features.Auth.Repositories;
using Luxira.Api.Features.Auth.Services;
using Luxira.Api.Features.Auth.Models;
using Microsoft.AspNetCore.Identity;

namespace Luxira.Api.Features.Auth;

public class AuthModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddLuxiraAuthentication(configuration, environment);

        services.AddScoped<UserRepository>();
        services.AddScoped<IPasswordHasher<ApplicationUser>, PasswordHasher<ApplicationUser>>();
        services.AddScoped<JwtService>();
        services.AddScoped<AuthCookieService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
    }
}
