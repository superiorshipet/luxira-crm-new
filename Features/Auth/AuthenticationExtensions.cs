using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Luxira.Api.Features.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddLuxiraAuthentication(
        this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<IConfiguration, IHostEnvironment>(
                (options, configuration, environment) =>
            {
                var jwtSettings = ResolveJwtSettings(
                    configuration,
                    environment);
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(jwtSettings.Key),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role",
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var detail = environment.IsEnvironment("Testing") &&
                            context.AuthenticateFailure is not null
                                ? context.AuthenticateFailure.Message
                                : "A valid access token is required.";
                        await Results.Problem(
                                statusCode: StatusCodes.Status401Unauthorized,
                                title: "Unauthorized",
                                detail: detail)
                            .ExecuteAsync(context.HttpContext);
                    },
                    OnForbidden = async context =>
                    {
                        await Results.Problem(
                                statusCode: StatusCodes.Status403Forbidden,
                                title: "Forbidden",
                                detail: "The authenticated identity does not have permission for this operation.")
                            .ExecuteAsync(context.HttpContext);
                    },
                };
            });

        services.AddHostedService<JwtConfigurationStartupValidator>();
        services.AddTransient<IClaimsTransformation, RoleAliasClaimsTransformation>();

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build())
            .AddPolicy(
                LuxiraPolicies.ReadOrderStatuses,
                policy => policy.RequireRole(
                    "Admin",
                    "ExecutiveDirector",
                    "DeliveryCompany",
                    "DeliveryRepresentative",
                    "CallCenter",
                    "FollowUpDepartment"));

        return services;
    }

    private static JwtSettings ResolveJwtSettings(
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var issuer = configuration["Jwt:Issuer"];
        var audience = configuration["Jwt:Audience"];
        var key = configuration["Jwt:Key"];

        if (!string.IsNullOrWhiteSpace(issuer) &&
            !string.IsNullOrWhiteSpace(audience) &&
            !string.IsNullOrWhiteSpace(key) &&
            Encoding.UTF8.GetByteCount(key) >= 32)
        {
            return new JwtSettings(
                issuer,
                audience,
                Encoding.UTF8.GetBytes(key));
        }

        if (!environment.IsDevelopment() &&
            !environment.IsEnvironment("Testing"))
        {
            throw new InvalidOperationException(
                "Jwt:Issuer, Jwt:Audience, and a Jwt:Key of at least 32 UTF-8 bytes are required outside Development/Testing.");
        }

        return new JwtSettings(
            "Luxira.Local",
            "Luxira.Local.Clients",
            RandomNumberGenerator.GetBytes(32));
    }

    private sealed record JwtSettings(
        string Issuer,
        string Audience,
        byte[] Key);

    private sealed class JwtConfigurationStartupValidator(
        IOptionsMonitor<JwtBearerOptions> options) : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = options.Get(JwtBearerDefaults.AuthenticationScheme);
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class RoleAliasClaimsTransformation : IClaimsTransformation
    {
        public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity is ClaimsIdentity identity)
            {
                if (principal.IsInRole("Admin") || principal.IsInRole("Administrator"))
                {
                    if (!principal.IsInRole("Admin")) identity.AddClaim(new Claim(identity.RoleClaimType, "Admin"));
                    if (!principal.IsInRole("Administrator")) identity.AddClaim(new Claim(identity.RoleClaimType, "Administrator"));
                    if (!principal.IsInRole("ExecutiveDirector")) identity.AddClaim(new Claim(identity.RoleClaimType, "ExecutiveDirector"));
                }
                if (principal.IsInRole("Team Leader") && !principal.IsInRole("TeamLeader"))
                {
                    identity.AddClaim(new Claim(identity.RoleClaimType, "TeamLeader"));
                }
            }

            return Task.FromResult(principal);
        }
    }
}
