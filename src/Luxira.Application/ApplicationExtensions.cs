using Luxira.Application.Features.DeliveryCompanies.GetDeliveryPrice;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryCompanies;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryOptions;
using Luxira.Application.Features.DeliveryCompanies.ListDeliveryRepresentatives;
using Luxira.Application.Features.SearchKeywords.ListSearchKeywords;
using Microsoft.Extensions.DependencyInjection;

namespace Luxira.Application;

public static class ApplicationExtensions
{
    public static IServiceCollection AddLuxiraApplication(
        this IServiceCollection services)
    {
        services.AddScoped<ListDeliveryCompaniesService>();
        services.AddScoped<ListDeliveryRepresentativesService>();
        services.AddScoped<GetDeliveryPriceService>();
        services.AddScoped<ListDeliveryOptionsService>();
        services.AddScoped<ListSearchKeywordsService>();
        return services;
    }
}
