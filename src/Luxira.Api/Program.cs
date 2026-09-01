using Luxira.Api.Features.Platform;
using Luxira.Api.Features.ReferenceData.Countries;
using Luxira.Api.Features.ReferenceData.FailureReasons;
using Luxira.Api.Features.ReferenceData.OrderSources;
using Luxira.Api.Features.ReferenceData.OrderStatuses;
using Luxira.Api.Features.DeliveryCompanies.GetDeliveryPrice;
using Luxira.Api.Features.DeliveryCompanies.ListDeliveryCompanies;
using Luxira.Api.Features.DeliveryCompanies.ListDeliveryRepresentatives;
using Luxira.Api.Features.DeliveryCompanies.ListDeliveryOptions;
using Luxira.Api.Features.SearchKeywords.ListSearchKeywords;
using Luxira.Api.Features.SearchKeywords.GetSearchKeywordOptions;
using Luxira.Api.Features.Identity.GetUserProfile;
using Luxira.Infrastructure;
using Luxira.Api.Authentication;
using Luxira.Api.OpenApi;
using Luxira.ServiceDefaults;
using Luxira.Application;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddLuxiraOpenApi();
builder.Services.AddLuxiraAuthentication();
builder.Services.AddLuxiraApplication();
builder.Services.AddLuxiraObservability(
    builder.Configuration,
    builder.Environment,
    "Luxira.Api");
builder.Services.AddLuxiraReadInfrastructure(
    builder.Configuration,
    builder.Environment);
builder.Services.AddResponseCompression(options =>
    options.EnableForHttps = true);
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy(
        "ReferenceData",
        policy => policy
            .Expire(TimeSpan.FromHours(24))
            .Tag("ReferenceData"));
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseResponseCompression();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapLuxiraOpenApi();
app.MapPlatformEndpoints();
app.MapCountryController();
app.MapFailureReasonController();
app.MapOrderSourceEndpoints();
app.MapOrderStatusEndpoints();
app.MapDeliveryCompanyController();
app.MapDeliveryRepresentativeController();
app.MapDeliveryPriceController();
app.MapDeliveryOptionController();
app.MapSearchKeywordController();
app.MapSearchKeywordOptionController();
app.MapUserProfileController();

app.Run();

public partial class Program;
