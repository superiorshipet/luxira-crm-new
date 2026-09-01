using Luxira.Api.Core;
using Luxira.Api.Features.DeliveryCompanies.Repositories;
using Luxira.Api.Features.DeliveryCompanies.Services;

namespace Luxira.Api.Features.DeliveryCompanies;

public class DeliveryCompanyModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<DeliveryCompanyRepository>();
        services.AddScoped<DeliveryCompanyService>();
    }
}
