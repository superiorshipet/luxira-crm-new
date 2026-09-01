using Luxira.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: Luxira.SchemaMigrator <repository-root> <sql-file>");
    return 2;
}

var repositoryRoot = Path.GetFullPath(args[0]);
var sqlFile = Path.GetFullPath(args[1]);
var environmentName = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";
var configuration = new ConfigurationBuilder()
    .SetBasePath(repositoryRoot)
    .AddJsonFile("appsettings.json", optional: false)
    .AddJsonFile($"appsettings.{environmentName}.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var connectionString = configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("ConnectionStrings:DefaultConnection is required.");
    return 3;
}

var sql = await File.ReadAllTextAsync(sqlFile);
var forbiddenTokens = new[] { "DROP ", "DELETE ", "TRUNCATE ", "ALTER TABLE" };
if (forbiddenTokens.Any(token => sql.Contains(token, StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine("Migration rejected because it contains a destructive token.");
    return 4;
}

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString, sqlServer => sqlServer.CommandTimeout(60))
    .Options;
await using var context = new ApplicationDbContext(options);
await context.Database.OpenConnectionAsync();

var databaseName = context.Database.GetDbConnection().Database;
var allowedTarget = databaseName.Contains("dev", StringComparison.OrdinalIgnoreCase) ||
    databaseName.Contains("test", StringComparison.OrdinalIgnoreCase) ||
    databaseName.Contains("local", StringComparison.OrdinalIgnoreCase);
if (!allowedTarget)
{
    Console.Error.WriteLine($"Migration refused for non-development database: {databaseName}");
    return 5;
}

await context.Database.ExecuteSqlRawAsync(sql);
Console.WriteLine($"Applied additive schema migration to {databaseName}.");
return 0;
