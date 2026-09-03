using Luxira.Api.Core;
using Luxira.Api.Features.SearchKeywords.Repositories;
using Luxira.Api.Features.SearchKeywords.Services;

namespace Luxira.Api.Features.SearchKeywords;

public class SearchKeywordModule : IModule
{
    public void Register(IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddScoped<SearchKeywordRepository>();
        services.AddScoped<SearchKeywordService>();
        services.AddScoped<Services.ImageSearchService>();
        services.AddScoped<Services.ImageVisionService>();
    }
}
