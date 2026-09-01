using Luxira.Api.Core;
using Luxira.Api.Features.Auth.Repositories;
using Luxira.Api.Features.Auth.Services;

namespace Luxira.Api.Features.Auth;

public class AuthModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddLuxiraAuthentication();

        services.AddScoped<UserRepository>();
        services.AddScoped<JwtService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
    }
}
