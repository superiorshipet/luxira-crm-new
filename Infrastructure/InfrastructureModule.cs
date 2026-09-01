using Luxira.Api.Core;
using Luxira.Api.Features.Communication.Hubs;
using Luxira.Api.Features.ManufacturingCompanies.Hubs;
using Luxira.Api.Features.Orders.Hubs;
using Luxira.Api.Infrastructure.BackgroundServices;
using Luxira.Api.Infrastructure.Email;
using Luxira.Api.Infrastructure.Pdf;
using Luxira.Api.Infrastructure.S3;
using Luxira.Api.Infrastructure.Sms;
using Luxira.Api.Infrastructure.WhatsApp;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Luxira.Api.Infrastructure;

public class InfrastructureModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        // 1. AWS S3 Storage Service
        services.AddScoped<S3StorageService>();

        // Cache
        services.AddMemoryCache(opts => { opts.SizeLimit = 10_000; });
        services.AddSingleton<Luxira.Api.Infrastructure.Caching.LuxiraCacheService>();

        // 2. WhatsApp Services (Lavva Cloud API + Infobip)
        services.AddHttpClient<LavvaWhatsAppService>(client =>
            client.Timeout = TimeSpan.FromSeconds(20));
        services.AddHttpClient<WhatsAppAutomationService>(client =>
            client.Timeout = TimeSpan.FromSeconds(20));

        // 3. SMS Service (Infobip)
        services.AddHttpClient<InfobipSmsService>(client =>
            client.Timeout = TimeSpan.FromSeconds(20));

        // 4. Email / SMTP Service (Gmail Smtp)
        services.AddScoped<LuxiraEmailService>();

        // 5. PDF Generation Service (QuestPDF)
        services.AddSingleton<LuxiraPdfService>();

        // 6. SignalR Real-Time Hubs
        services.AddSignalR();

        // 7. Hosted Background Services
        var backgroundJobsEnabled = configuration
            .GetValue<bool?>("BackgroundJobs:Enabled") ??
            environment.IsProduction();
        if (backgroundJobsEnabled)
        {
            services.AddHostedService<DeliveredToBalanceAutoTransitionBackgroundService>();
            services.AddHostedService<PendingDownloadReminderBackgroundService>();
            services.AddHostedService<StoreInvoiceDailyEmailService>();
            services.AddHostedService<ScreenRecordCleanupService>();
        }
    }

    public void Configure(IApplicationBuilder app)
    {
        if (app is IEndpointRouteBuilder endpoints)
        {
            endpoints.MapHub<OrderHub>("/hubs/orders");
            endpoints.MapHub<MessageHub>("/hubs/messages");
            endpoints.MapHub<ConferenceHub>("/hubs/conference");
            endpoints.MapHub<StoreCodeEditorHub>("/hubs/store-code-editor");
        }
    }
}
