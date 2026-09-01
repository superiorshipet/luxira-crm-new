using Luxira.Api.Core;
using Luxira.Api.Features.Warehouses.Repositories;
using Luxira.Api.Features.Warehouses.Services;

namespace Luxira.Api.Features.Warehouses;

public class WarehouseModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<WarehouseRepository>();
        services.AddScoped<WarehouseService>();
    }
}
