using Luxira.Api.Core;
using Luxira.Api.Data;
using Luxira.Api.OpenApi;
using Luxira.Api.Utils.Middlewares;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using System.Threading.RateLimiting;

var builder = WebApplication.CreateBuilder(args);

// ─── Database Context ────────────────────────────────────────────────────────
// Uses the existing DB schema — NO migrations are applied at startup.
// The connection string points to a live SQL Server DB with all tables already created.
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

        options
            .UseSqlServer(connectionString, sqlOptions =>
            {
                // 30-second command timeout
                sqlOptions.CommandTimeout(30);
                // Auto-retry transient failures up to 3 times with 5s wait
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null);
                // Use query splitting for large collection navigations (avoids cartesian explosion)
                sqlOptions.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
            })
            .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
            .EnableDetailedErrors(builder.Environment.IsDevelopment());
    }
}, poolSize: 128);  // Pool of 128 DB context instances for high concurrency

// ─── In-Process Cache (lookup tables: delivery companies, stores, etc.) ──────
builder.Services.AddMemoryCache(opts =>
{
    opts.SizeLimit = 10_000;          // Max 10 000 cache "size units"
    opts.CompactionPercentage = 0.1;  // Remove 10% of entries when limit is hit
});
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".Luxira.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(8);
});

// ─── Dynamic Discovery: all IModule implementations ──────────────────────────
var modules = typeof(Program).Assembly.GetTypes()
    .Where(t => typeof(IModule).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
    .Select(t => (IModule)Activator.CreateInstance(t)!)
    .ToList();

foreach (var module in modules)
{
    module.Register(builder.Services, builder.Configuration, builder.Environment);
}

// ─── Controllers & API Explorer ───────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddLuxiraOpenApi();

// ─── Response Compression ─────────────────────────────────────────────────────
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});

// ─── Response Caching (for public GET endpoints with [ResponseCache]) ─────────
builder.Services.AddResponseCaching();

// ─── Rate Limiting ────────────────────────────────────────────────────────────
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

// ─── CORS ─────────────────────────────────────────────────────────────────────
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

// Health Checks registered by PlatformModule (DatabaseHealthCheck with "database" tag)

// ════════════════════════════════════════════════════════════════════════════════
var app = builder.Build();
// ════════════════════════════════════════════════════════════════════════════════

// Global Exception Handling
app.UseMiddleware<ExceptionHandlingMiddleware>();

// Compression must come before response caching
app.UseResponseCompression();

// Response caching (respects [ResponseCache] attributes on controllers)
app.UseResponseCaching();

// OpenAPI / Swagger
app.MapLuxiraOpenApi();

app.UseRouting();
app.UseSession();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Configure Feature Modules (SignalR hubs, etc.)
foreach (var module in modules)
{
    module.Configure(app);
}

// Map all Feature Controllers
app.MapControllers();

// Health endpoint
app.MapHealthChecks("/health");

app.Run();

public partial class Program;
