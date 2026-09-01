namespace Luxira.Api.Core;

public interface IModule
{
    void Register(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment
    );

    void Configure(WebApplication app)
    {
        // Default implementation does nothing
    }
}
