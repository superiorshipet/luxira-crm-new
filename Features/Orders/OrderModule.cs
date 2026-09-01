using Luxira.Api.Core;
using Luxira.Api.Features.Orders.Repositories;
using Luxira.Api.Features.Orders.Services;

namespace Luxira.Api.Features.Orders;

public class OrderModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<OrderRepository>();
        services.AddSingleton<OrderStatusTransitionPolicy>();
        services.AddScoped<OrderService>();
    }
}
