using Luxira.Api.Core;
using Luxira.Api.Features.Expenses.Repositories;
using Luxira.Api.Features.Expenses.Services;

namespace Luxira.Api.Features.Expenses;

public class ExpenseModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<ExpenseRepository>();
        services.AddScoped<ExpenseService>();
    }
}
