using System.Text.RegularExpressions;

using Microsoft.Data.SqlClient;

using Testcontainers.MsSql;

namespace MOConverter.Tests;

[CollectionDefinition(Name)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServer>
{
    public const string Name = "SQL Server";
}

/// <summary>
/// SQL Server in a container: 2025 unless MSSQL_IMAGE names another image, as CI does for 2022.
/// It has two databases: Cases, built from Fixtures/cases*.sql, and Probe, which has a
/// memory-optimized filegroup for trying what memory-optimized tables accept.
/// </summary>
public sealed partial class SqlServer : IAsyncLifetime
{
    public const string Cases = "Cases";
    public const string Probe = "Probe";

    private static readonly string Image =
        Environment.GetEnvironmentVariable("MSSQL_IMAGE") ?? "mcr.microsoft.com/mssql/server:2025-latest";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    public int MajorVersion { get; private set; }

    public async ValueTask InitializeAsync()
    {
        try
        {
            await _container.StartAsync();
        }
        catch (Exception e)
        {
            // Show why SQL Server didn't start, not only that it didn't
            var (stdout, stderr) = await _container.GetLogsAsync();
            throw new InvalidOperationException($"SQL Server didn't start:\n{stdout}\n{stderr}", e);
        }

        MajorVersion = Convert.ToInt32(await ScalarAsync("master", "SELECT SERVERPROPERTY('ProductMajorVersion');"));

        await ExecuteAsync("master", $"""
            CREATE DATABASE {Cases};
            ALTER DATABASE {Cases} SET CHANGE_TRACKING = ON;
            CREATE DATABASE {Probe};
            ALTER DATABASE {Probe} ADD FILEGROUP mo CONTAINS MEMORY_OPTIMIZED_DATA;
            DECLARE @path nvarchar(400) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400)) + N'{Probe}_mo';
            EXEC (N'ALTER DATABASE {Probe} ADD FILE (NAME = ''{Probe}_mo'', FILENAME = ''' + @path + N''') TO FILEGROUP mo;');
            """);

        await RunScriptAsync(Cases, Fixture("cases.sql"));
        if (MajorVersion >= 16)
            await RunScriptAsync(Cases, Fixture("cases-2022.sql"));
        if (MajorVersion >= 17)
            await RunScriptAsync(Cases, Fixture("cases-2025.sql"));
    }

    public string ConnectionString(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ToString();

    public async Task ExecuteAsync(string database, string sql)
    {
        await using var connection = new SqlConnection(ConnectionString(database));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        await command.ExecuteNonQueryAsync();
    }

    public async Task<object?> ScalarAsync(string database, string sql)
    {
        await using var connection = new SqlConnection(ConnectionString(database));
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection) { CommandTimeout = 120 };
        return await command.ExecuteScalarAsync();
    }

    /// <summary>Runs a script of batches separated by GO, as SSMS and sqlcmd do.</summary>
    public async Task RunScriptAsync(string database, string script)
    {
        foreach (var batch in Go().Split(script).Where(b => b.Trim().Length > 0))
            await ExecuteAsync(database, batch);
    }

    /// <summary>Runs a statement and returns the error it raised, or null if it worked.</summary>
    public async Task<SqlException?> TryAsync(string database, string sql)
    {
        try
        {
            await ExecuteAsync(database, sql);
            return null;
        }
        catch (SqlException e)
        {
            return e;
        }
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    private static string Fixture(string name) => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex Go();
}
