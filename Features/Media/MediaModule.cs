using Luxira.Api.Core;
using Luxira.Api.Features.Media.Repositories;
using Luxira.Api.Features.Media.Services;

namespace Luxira.Api.Features.Media;

public class MediaModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<MediaRepository>();
        services.AddScoped<MediaService>();
        services.AddScoped<MediaMigrationService>();
        services.AddScoped<MediaReferenceCleanupService>();
        if (!environment.IsEnvironment("Testing") && configuration.GetValue<bool>("BackgroundJobs:Enabled"))
            services.AddHostedService<MediaReferenceCleanupBackgroundService>();
    }
}
