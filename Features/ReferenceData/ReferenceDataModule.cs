using Luxira.Api.Core;

namespace Luxira.Api.Features.ReferenceData;

public class ReferenceDataModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Reference data catalogs are static/singleton
    }

    public void Configure(WebApplication app)
    {
        // Default endpoint mappings are supported via MapControllers() and minimal endpoint extensions
    }
}
