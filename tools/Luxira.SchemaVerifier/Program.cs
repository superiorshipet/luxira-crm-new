using System.Data.Common;
using Luxira.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

var repositoryRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();
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
    return 2;
}

var options = new DbContextOptionsBuilder<ApplicationDbContext>()
    .UseSqlServer(connectionString, sql => sql.CommandTimeout(30))
    .Options;
await using var context = new ApplicationDbContext(options);

try
{
    await context.Database.OpenConnectionAsync();
}
catch (Exception exception)
{
    Console.Error.WriteLine($"Could not connect to the configured database: {exception.GetType().Name}.");
    return 3;
}

var databaseColumns = await ReadColumnsAsync(context.Database.GetDbConnection());
Console.WriteLine($"Database target: {context.Database.GetDbConnection().Database}");
var relationalModel = context.Model.GetRelationalModel();
var missingTables = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
var missingColumns = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
var mismatchedTableKeys = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var table in relationalModel.Tables)
{
    var schema = table.Schema ?? "dbo";
    var tableKey = $"{schema}.{table.Name}";
    var matchingColumns = databaseColumns
        .Where(column => column.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase)
            && column.Table.Equals(table.Name, StringComparison.OrdinalIgnoreCase))
        .Select(column => column.Column)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    if (matchingColumns.Count == 0)
    {
        missingTables.Add(tableKey);
        mismatchedTableKeys.Add(tableKey);
        continue;
    }

    foreach (var column in table.Columns)
    {
        if (!matchingColumns.Contains(column.Name))
        {
            missingColumns.Add($"{tableKey}.{column.Name}");
            mismatchedTableKeys.Add(tableKey);
        }
    }
}

Console.WriteLine($"Mapped tables checked: {relationalModel.Tables.Count()}");
Console.WriteLine($"Missing tables: {missingTables.Count}");
foreach (var table in missingTables) Console.WriteLine($"TABLE {table}");
Console.WriteLine($"Missing columns: {missingColumns.Count}");
foreach (var column in missingColumns) Console.WriteLine($"COLUMN {column}");
Console.WriteLine("Actual columns for mismatched existing tables:");
foreach (var tableKey in mismatchedTableKeys.Except(missingTables, StringComparer.OrdinalIgnoreCase))
{
    var separator = tableKey.IndexOf('.');
    var schema = tableKey[..separator];
    var table = tableKey[(separator + 1)..];
    var actual = databaseColumns
        .Where(column => column.Schema.Equals(schema, StringComparison.OrdinalIgnoreCase)
            && column.Table.Equals(table, StringComparison.OrdinalIgnoreCase))
        .Select(column => column.Column);
    Console.WriteLine($"ACTUAL {tableKey}: {string.Join(',', actual)}");
}

return missingTables.Count == 0 && missingColumns.Count == 0 ? 0 : 1;

static async Task<List<DatabaseColumn>> ReadColumnsAsync(DbConnection connection)
{
    await using var command = connection.CreateCommand();
    command.CommandText = """
        SELECT TABLE_SCHEMA, TABLE_NAME, COLUMN_NAME
        FROM INFORMATION_SCHEMA.COLUMNS
        ORDER BY TABLE_SCHEMA, TABLE_NAME, ORDINAL_POSITION;
        """;
    var columns = new List<DatabaseColumn>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        columns.Add(new DatabaseColumn(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2)));
    }
    return columns;
}

internal sealed record DatabaseColumn(string Schema, string Table, string Column);
