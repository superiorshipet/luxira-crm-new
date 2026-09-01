using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Luxira.Api.Features.Auth;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddLuxiraAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSingleton(
            JwtSigningMaterial.Create(configuration, environment));

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(
                JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtSigningMaterial, IHostEnvironment>(
                (options, jwt, hostEnvironment) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(jwt.Key),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    NameClaimType = JwtRegisteredClaimNames.Sub,
                    RoleClaimType = "role",
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        if (!string.IsNullOrEmpty(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments("/hubs"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        var detail = hostEnvironment.IsEnvironment("Testing") &&
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
