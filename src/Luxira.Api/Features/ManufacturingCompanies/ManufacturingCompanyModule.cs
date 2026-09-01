using Luxira.Api.Core;
using Luxira.Api.Features.ManufacturingCompanies.Repositories;
using Luxira.Api.Features.ManufacturingCompanies.Services;

namespace Luxira.Api.Features.ManufacturingCompanies;

public class ManufacturingCompanyModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<ManufacturingCompanyRepository>();
        services.AddScoped<ManufacturingCompanyService>();
    }
}
