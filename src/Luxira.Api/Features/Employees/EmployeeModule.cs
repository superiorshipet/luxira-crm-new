using Luxira.Api.Core;
using Luxira.Api.Features.Employees.Repositories;
using Luxira.Api.Features.Employees.Services;

namespace Luxira.Api.Features.Employees;

public class EmployeeModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<EmployeeRepository>();
        services.AddScoped<EmployeeService>();
    }
}
