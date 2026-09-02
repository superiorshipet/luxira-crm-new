using Luxira.Api.Core;
using Luxira.Api.Features.Communication.Hubs;
using Luxira.Api.Features.ManufacturingCompanies.Hubs;
using Luxira.Api.Features.Orders.Hubs;

namespace Luxira.Api.Features.Communication;

public sealed class RealtimeModule : IModule
{
    public void Register(
        IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddSignalR();
    }

    public void Configure(WebApplication app)
    {
        app.MapHub<OrderHub>("/orderHub");
        app.MapHub<MessageHub>("/messageHub");
        app.MapHub<StoreCodeEditorHub>("/storeCodeEditorHub");
        app.MapHub<ConferenceHub>("/conferenceHub");
    }
}
