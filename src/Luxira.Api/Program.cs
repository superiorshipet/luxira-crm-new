using Luxira.Api.Features.Platform;
using Luxira.Api.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddLuxiraOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseHttpsRedirection();

app.MapLuxiraOpenApi();
app.MapPlatformEndpoints();

app.Run();

public partial class Program;

