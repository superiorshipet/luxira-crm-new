using System.Text;
using Luxira.Api.Core;
using Luxira.Api.Features.Auth.Repositories;
using Luxira.Api.Features.Auth.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Luxira.Api.Features.Auth;

public class AuthModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var key = configuration["Jwt:Key"] ?? "super-secret-default-key-luxira-crm-jwt-secret-2026-auth";
        var issuer = configuration["Jwt:Issuer"] ?? "Luxira.Api";
        var audience = configuration["Jwt:Audience"] ?? "Luxira.Clients";

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        })
        .AddJwtBearer(options =>
        {
            options.RequireHttpsMetadata = false;
            options.SaveToken = true;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudience = audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                ClockSkew = TimeSpan.Zero
            };
        });

        services.AddAuthorization();

        services.AddScoped<UserRepository>();
        services.AddScoped<JwtService>();
        services.AddScoped<AuthService>();
        services.AddScoped<UserService>();
    }
}
