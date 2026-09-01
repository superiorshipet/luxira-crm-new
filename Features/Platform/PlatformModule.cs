using Luxira.Api.Core;

namespace Luxira.Api.Features.Platform;

public class PlatformModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
    }

    public void Configure(WebApplication app)
    {
        app.MapPlatformEndpoints();
    }
}
