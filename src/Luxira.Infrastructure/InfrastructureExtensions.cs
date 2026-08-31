using Luxira.Infrastructure.DeliveryCompanies;
using Luxira.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Luxira.Infrastructure;

public static class InfrastructureExtensions
{
    public static IServiceCollection AddLuxiraReadInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        services.AddHybridCache();

        var sqlConnection = configuration.GetConnectionString("LuxiraSqlServer");
        if (string.IsNullOrWhiteSpace(sqlConnection))
        {
            if (!environment.IsDevelopment() &&
                !environment.IsEnvironment("Testing"))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:LuxiraSqlServer is required outside Development/Testing.");
            }

            services.AddSingleton<
                IDeliveryCompanyReader,
                UnavailableDeliveryCompanyReader>();
            return services;
        }

        services.AddPooledDbContextFactory<LuxiraReadDbContext>(options =>
            options
                .UseSqlServer(
                    sqlConnection,
                    sql => sql
                        .EnableRetryOnFailure(3)
                        .CommandTimeout(30))
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking));
        services.AddScoped<IDeliveryCompanyReader, SqlDeliveryCompanyReader>();

        var redisConnection = configuration.GetConnectionString("LuxiraRedis");
        if (!string.IsNullOrWhiteSpace(redisConnection))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnection;
                options.InstanceName = "luxira:v1:";
            });
        }

        return services;
    }
}
