using Luxira.Api.Core;
using Luxira.Api.Data;
using Luxira.Api.OpenApi;
using Luxira.Api.Utils.Middlewares;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// Database Context (SQL Server with InMemory support for Testing)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing") || 
        string.IsNullOrEmpty(connectionString) || 
        connectionString.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase))
    {
        var dbName = connectionString?.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase) == true 
            ? connectionString["InMemory:".Length..] 
            : "LuxiraTestDb";
        options.UseInMemoryDatabase(dbName);
    }
    else
    {
        options.UseSqlServer(connectionString);
    }
});

// Dynamic Discovery and Registration of all Feature Modules
var modules = typeof(Program).Assembly.GetTypes()
    .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
    .Select(t => (IModule)Activator.CreateInstance(t)!)
    .ToList();

foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration, builder.Environment);
}

// Controller & API Explorer Configuration
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddLuxiraOpenApi();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();

// OpenAPI / Swagger Documentation
app.MapLuxiraOpenApi();

app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

// Configure Feature Modules
foreach (var module in modules)
{
    module.Configure(app);
}

// Map all Feature Controllers
app.MapControllers();

app.Run();


public partial class Program;
