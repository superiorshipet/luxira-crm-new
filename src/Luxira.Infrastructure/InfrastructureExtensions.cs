using Luxira.Application.Features.DeliveryCompanies.GetDeliveryPrice;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryCompanies;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryRepresentatives;
using Luxira.Infrastructure.Features.DeliveryCompanies.GetDeliveryPrice;
using Luxira.Infrastructure.Features.DeliveryCompanies.ListDeliveryCompanies;
using Luxira.Infrastructure.Features.DeliveryCompanies.ListDeliveryOptions;
using Luxira.Infrastructure.Features.DeliveryCompanies.ListDeliveryRepresentatives;
using Luxira.Application.Features.SearchKeywords.ListSearchKeywords;
using Luxira.Infrastructure.Features.SearchKeywords.ListSearchKeywords;
using Luxira.Application.Features.SearchKeywords.GetSearchKeywordOptions;
using Luxira.Infrastructure.Features.SearchKeywords.GetSearchKeywordOptions;
using Luxira.Application.Features.Identity.GetUserProfile;
using Luxira.Infrastructure.Features.Identity.GetUserProfile;
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
                IListDeliveryCompaniesRepository,
                UnavailableListDeliveryCompaniesRepository>();
            services.AddSingleton<
                IListDeliveryRepresentativesRepository,
                UnavailableListDeliveryRepresentativesRepository>();
            services.AddSingleton<
                IGetDeliveryPriceRepository,
                UnavailableGetDeliveryPriceRepository>();
            services.AddSingleton<
                IListDeliveryOptionsRepository,
                UnavailableListDeliveryOptionsRepository>();
            services.AddSingleton<
                IListSearchKeywordsRepository,
                UnavailableListSearchKeywordsRepository>();
            services.AddSingleton<
                IGetSearchKeywordOptionsRepository,
                UnavailableGetSearchKeywordOptionsRepository>();
            services.AddSingleton<
                IGetUserProfileRepository,
                UnavailableGetUserProfileRepository>();
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
        services.AddScoped<
            IListDeliveryCompaniesRepository,
            SqlListDeliveryCompaniesRepository>();
        services.AddScoped<
            IListDeliveryRepresentativesRepository,
            SqlListDeliveryRepresentativesRepository>();
        services.AddScoped<
            IGetDeliveryPriceRepository,
            SqlGetDeliveryPriceRepository>();
        services.AddScoped<
            IListDeliveryOptionsRepository,
            SqlListDeliveryOptionsRepository>();
        services.AddScoped<
            IListSearchKeywordsRepository,
            SqlListSearchKeywordsRepository>();
        services.AddScoped<
            IGetSearchKeywordOptionsRepository,
            SqlGetSearchKeywordOptionsRepository>();
        services.AddScoped<
            IGetUserProfileRepository,
            SqlGetUserProfileRepository>();

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
