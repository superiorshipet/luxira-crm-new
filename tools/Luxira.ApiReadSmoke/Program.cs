using System.Globalization;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Luxira.Api.Data;
using Luxira.Api.Features.Auth.Models;
using Luxira.Api.Features.Auth.Services;
using Luxira.Api.Features.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

if (args.Length is < 2 or > 3)
{
    Console.Error.WriteLine("Usage: Luxira.ApiReadSmoke <repository-root> <base-url> [route-contains]");
    return 2;
}

var repositoryRoot = Path.GetFullPath(args[0]);
var baseUrl = args[1].TrimEnd('/');
var routeFilter = args.Length == 3 ? args[2] : null;
var configuration = new ConfigurationBuilder()
    .SetBasePath(repositoryRoot)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile("appsettings.Development.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var configuredKey = configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(configuredKey) || Encoding.UTF8.GetByteCount(configuredKey) < 32)
{
    Console.Error.WriteLine("Read smoke refused because Development does not use a stable configured JWT key.");
    return 3;
}

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings:DefaultConnection is required.");
    return 4;
}

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString)
    .Options;
await using var context = new ApplicationDbContext(options);

var admin = await (
    from user in context.Users.AsNoTracking()
    join userRole in context.UserRoles.AsNoTracking() on user.Id equals userRole.UserId
    join role in context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
    where role.Name == "Admin" || role.Name == "Administrator"
    select new ApplicationUser
    {
        Id = user.Id,
        UserName = user.UserName,
        Email = user.Email,
        AcessId = user.AcessId,
        Country = user.Country,
        Role = "Admin"
    }).FirstOrDefaultAsync();

if (admin is null)
{
    Console.Error.WriteLine("No administrator user is available for authenticated read smoke checks.");
    return 5;
}

var signingMaterial = JwtSigningMaterial.Create(configuration, new DevelopmentEnvironment(repositoryRoot));
var (token, _) = new JwtService(signingMaterial).GenerateToken(admin);

using var client = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = Timeout.InfiniteTimeSpan };
client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

var realtimeFailures = new List<string>();
var hubPaths = new[] { "/orderHub", "/messageHub", "/storeCodeEditorHub", "/conferenceHub" };
using (var hubClient = new HttpClient
{
    BaseAddress = new Uri(baseUrl),
    Timeout = TimeSpan.FromSeconds(15)
})
{
    foreach (var hubPath in hubPaths)
    {
        using var content = new StringContent(string.Empty);
        using var response = await hubClient.PostAsync(
            $"{hubPath}/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}",
            content);
        if (!response.IsSuccessStatusCode)
            realtimeFailures.Add($"{(int)response.StatusCode} {hubPath}");
    }
}

using var openApiResponse = await client.GetAsync("/swagger/v1/swagger.json");
openApiResponse.EnsureSuccessStatusCode();
await using var openApiStream = await openApiResponse.Content.ReadAsStreamAsync();
using var document = await JsonDocument.ParseAsync(openApiStream);

var checkedRoutes = 0;
var serverFailures = new List<string>();
var timedOutRoutes = new List<string>();
var routeDurations = new List<double>();
var totalWatch = Stopwatch.StartNew();
foreach (var pathProperty in document.RootElement.GetProperty("paths").EnumerateObject())
{
    if (!pathProperty.Name.StartsWith("/api/v1", StringComparison.OrdinalIgnoreCase) ||
        (routeFilter is not null && !pathProperty.Name.Contains(routeFilter, StringComparison.OrdinalIgnoreCase)) ||
        !pathProperty.Value.TryGetProperty("get", out var operation))
    {
        continue;
    }

    var route = ReplacePathParameters(pathProperty.Name);
    var query = BuildRequiredQuery(operation);
    checkedRoutes++;
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    try
    {
        var routeWatch = Stopwatch.StartNew();
        using var response = await client.GetAsync(
            route + query,
            HttpCompletionOption.ResponseHeadersRead,
            timeout.Token);
        routeWatch.Stop();
        routeDurations.Add(routeWatch.Elapsed.TotalMilliseconds);
        if ((int)response.StatusCode >= 500)
            serverFailures.Add($"{(int)response.StatusCode} {route}");
    }
    catch (OperationCanceledException) when (timeout.IsCancellationRequested)
    {
        timedOutRoutes.Add(route);
    }
}
totalWatch.Stop();

Console.WriteLine($"Authenticated canonical GET checks: {checkedRoutes}");
Console.WriteLine($"Authenticated SignalR negotiate checks: {hubPaths.Length}");
Console.WriteLine($"SignalR failures: {realtimeFailures.Count}");
foreach (var failure in realtimeFailures) Console.WriteLine(failure);
Console.WriteLine($"Server failures: {serverFailures.Count}");
foreach (var failure in serverFailures) Console.WriteLine(failure);
Console.WriteLine($"Timed out routes: {timedOutRoutes.Count}");
foreach (var route in timedOutRoutes) Console.WriteLine($"TIMEOUT {route}");
if (routeDurations.Count > 0)
{
    routeDurations.Sort();
    Console.WriteLine($"GET latency ms: p50={Percentile(routeDurations, 0.50):0.0}, p95={Percentile(routeDurations, 0.95):0.0}, max={routeDurations[^1]:0.0}, total={totalWatch.Elapsed.TotalMilliseconds:0.0}");
}
return realtimeFailures.Count == 0 && serverFailures.Count == 0 && timedOutRoutes.Count == 0
    ? 0
    : 1;

static double Percentile(IReadOnlyList<double> sorted, double percentile) =>
    sorted[Math.Clamp((int)Math.Ceiling(sorted.Count * percentile) - 1, 0, sorted.Count - 1)];

static string ReplacePathParameters(string route) =>
    Regex.Replace(route, "\\{[^}:]+(?::[^}]+)?\\}", "1");

static string BuildRequiredQuery(JsonElement operation)
{
    if (!operation.TryGetProperty("parameters", out var parameters)) return string.Empty;

    var values = new List<string>();
    foreach (var parameter in parameters.EnumerateArray())
    {
        if (!parameter.TryGetProperty("in", out var location) || location.GetString() != "query" ||
            !parameter.TryGetProperty("required", out var required) || !required.GetBoolean())
        {
            continue;
        }

        var name = parameter.GetProperty("name").GetString()!;
        var value = "1";
        if (parameter.TryGetProperty("schema", out var schema))
        {
            if (schema.TryGetProperty("format", out var format) && format.GetString() == "date-time")
                value = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
            else if (schema.TryGetProperty("type", out var type) && type.GetString() == "boolean")
                value = "true";
        }

        values.Add($"{Uri.EscapeDataString(name)}={Uri.EscapeDataString(value)}");
    }

    return values.Count == 0 ? string.Empty : $"?{string.Join('&', values)}";
}

file sealed class DevelopmentEnvironment(string contentRoot) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ApplicationName { get; set; } = "Luxira.ApiReadSmoke";
    public string ContentRootPath { get; set; } = contentRoot;
    public IFileProvider ContentRootFileProvider { get; set; } = new PhysicalFileProvider(contentRoot);
}
