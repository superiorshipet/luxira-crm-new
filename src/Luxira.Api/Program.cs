using Luxira.Api.Features.Platform;
using Luxira.Api.Features.ReferenceData.Countries;
using Luxira.Api.Features.ReferenceData.FailureReasons;
using Luxira.Api.Features.ReferenceData.OrderSources;
using Luxira.Api.Features.ReferenceData.OrderStatuses;
using Luxira.Api.Features.DeliveryCompanies.GetDeliveryPrice;
using Luxira.Api.Features.DeliveryCompanies.ListDeliveryCompanies;
using Luxira.Api.Features.DeliveryCompanies.ListDeliveryRepresentatives;
using Luxira.Api.Features.DeliveryCompanies.ListDeliveryOptions;
using Luxira.Infrastructure;
using Luxira.Api.Authentication;
using Luxira.Api.OpenApi;
using Luxira.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddLuxiraOpenApi();
builder.Services.AddLuxiraAuthentication();
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
app.MapCountryEndpoints();
app.MapFailureReasonEndpoints();
app.MapOrderSourceEndpoints();
app.MapOrderStatusEndpoints();
app.MapDeliveryCompanyEndpoints();
app.MapDeliveryRepresentativeEndpoints();
app.MapDeliveryPriceEndpoints();
app.MapDeliveryOptionEndpoints();

app.Run();

public partial class Program;
