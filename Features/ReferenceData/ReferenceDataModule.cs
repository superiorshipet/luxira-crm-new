using Luxira.Api.Core;
using Luxira.Api.Features.ReferenceData.Countries;
using Luxira.Api.Features.ReferenceData.FailureReasons;
using Luxira.Api.Features.ReferenceData.OrderSources;
using Luxira.Api.Features.ReferenceData.OrderStatuses;

namespace Luxira.Api.Features.ReferenceData;

public class ReferenceDataModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // Reference data catalogs are static/singleton
    }

    public void Configure(WebApplication app)
    {
        app.MapCountryController();
        app.MapFailureReasonController();
        app.MapOrderSourceEndpoints();
        app.MapOrderStatusEndpoints();
    }
}
