using Luxira.Api.Core;
using Luxira.Api.Features.Communication.Repositories;
using Luxira.Api.Features.Communication.Services;

namespace Luxira.Api.Features.Communication;

public class CommunicationModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<PasswordEmailRepository>();
        services.AddScoped<PasswordEmailService>();
    }
}
