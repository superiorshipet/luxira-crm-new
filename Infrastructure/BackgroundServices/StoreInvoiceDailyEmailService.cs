using Luxira.Api.Data;
using Luxira.Api.Infrastructure.Email;
using Luxira.Api.Utils.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Luxira.Api.Infrastructure.BackgroundServices;

public class StoreInvoiceDailyEmailService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<StoreInvoiceDailyEmailService> _logger;

    public StoreInvoiceDailyEmailService(
        IServiceScopeFactory scopeFactory,
        ILogger<StoreInvoiceDailyEmailService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var now = IstanbulTimeHelper.Now;
            // Run at 10:00 AM Istanbul time daily
            if (now.Hour == 10 && now.Minute < 5)
            {
                try
                {
                    await ProcessDailyInvoicesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to process daily store invoices.");
                }

                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }

    private async Task ProcessDailyInvoicesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<LuxiraEmailService>();

        var stores = await db.ManufacturingCompanies.AsNoTracking().ToListAsync(ct);
        var yesterday = IstanbulTimeHelper.Now.Date.AddDays(-1);

        foreach (var store in stores)
        {
            var ordersCount = await db.Orders
                .Where(o => o.ManufacturingCompanyId == store.Id && o.CreatedDate.Date == yesterday)
                .CountAsync(ct);

            if (ordersCount > 0)
            {
                var subject = $"التقرير اليومي لمبيعات متجر {store.Name} - {yesterday:yyyy-MM-dd}";
                var body = $"<h3>تقرير مبيعات متجر {store.Name}</h3><p>إجمالي الطلبات المستلمة بالأمس: <strong>{ordersCount}</strong> طلب.</p>";
                await emailService.SendEmailAsync(string.Empty, subject, body, null, null, ct);
            }
        }
    }
}
