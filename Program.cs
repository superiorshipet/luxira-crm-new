using Luxira.Api.Core;
using Luxira.Api.Data;
using Luxira.Api.OpenApi;
using Luxira.Api.Utils.Middlewares;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);

// Database Context (SQL Server with InMemory support for Testing)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContextPool<ApplicationDbContext>(options =>
{
    if (builder.Environment.IsEnvironment("Testing") ||
        connectionString?.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase) == true)
    {
        var dbName = connectionString?.StartsWith("InMemory:", StringComparison.OrdinalIgnoreCase) == true 
            ? connectionString["InMemory:".Length..] 
            : "LuxiraTestDb";
        options.UseInMemoryDatabase(dbName);
    }
    else
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is required outside Testing or an explicit InMemory configuration.");
        }

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
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(
        context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.Identity?.Name ??
                context.Connection.RemoteIpAddress?.ToString() ??
                "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 300,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 20,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            }));
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (builder.Environment.IsDevelopment() ||
            builder.Environment.IsEnvironment("Testing"))
        {
            policy.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
            return;
        }

        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];
        if (allowedOrigins.Length == 0)
        {
            throw new InvalidOperationException(
                "Cors:AllowedOrigins must contain at least one origin outside Development/Testing.");
        }

        policy.WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});

var app = builder.Build();

// Global Exception Handling Middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseResponseCompression();

// OpenAPI / Swagger Documentation
app.MapLuxiraOpenApi();

app.UseRouting();
app.UseCors();
app.UseRateLimiter();
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
