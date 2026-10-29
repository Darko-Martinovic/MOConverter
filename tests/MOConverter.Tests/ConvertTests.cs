using MOConverter.Assessment;
using MOConverter.Catalog;
using MOConverter.Cli;
using MOConverter.Conversion;
using MOConverter.Rules;

using Spectre.Console.Cli.Testing;

namespace MOConverter.Tests;

/// <summary>
/// Converts every table of Fixtures/cases.sql that isn't blocked, through the command line, and
/// reads the values back from the new database.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ConvertTests(SqlServer server)
{
    private const string Target = "Cases Converted";

    private static readonly SemaphoreSlim Once = new(1, 1);
    private static (CommandAppResult Result, IReadOnlyList<ObjectName> Tables)? converted;

    private static CommandAppResult Run(params string[] args)
    {
        var app = new CommandAppTester();
        app.Configure(App.Configure);
        return app.Run(args);
    }

    private async Task<(CommandAppResult Result, IReadOnlyList<ObjectName> Tables)> ConvertedAsync()
    {
        await Once.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (converted is { } done)
                return done;

            var assessment = await Assessor.AssessAsync(server.ConnectionString(SqlServer.Cases), Selection.Parse(null, "cases"), TestContext.Current.CancellationToken);
            var tables = assessment.Tables.Where(t => t.Outcome != Outcome.Blocked).Select(t => t.Name).ToList();
            var result = Run("convert", "--source", server.ConnectionString(SqlServer.Cases), "--target-db", Target,
                "--tables", string.Join(",", tables), "--accept-lossy", "--hash-pk", "cases.Ready", "--batch-size", "100");
            converted = (result, tables);
            return converted.Value;
        }
        finally
        {
            Once.Release();
        }
    }

    private async Task<object?> InTargetAsync(string sql)
    {
        var (result, _) = await ConvertedAsync();
        Assert.True(result.ExitCode == 0, result.Output);
        return await server.ScalarAsync(Target, sql);
    }

    [Fact]
    public async Task Every_table_is_copied_whole()
    {
        var (result, tables) = await ConvertedAsync();

        Assert.True(result.ExitCode == 0, result.Output);
        foreach (var table in tables)
        {
            var count = $"SELECT COUNT_BIG(*) FROM {table};";
            Assert.Equal(await server.ScalarAsync(SqlServer.Cases, count), await server.ScalarAsync(Target, count));
            Assert.Contains($"{table}:", result.Output);
        }

        Assert.Equal(1001L, await server.ScalarAsync(Target, "SELECT COUNT_BIG(*) FROM cases.Ready;"));
    }

    [Fact]
    public async Task Alias_types_keep_their_values()
    {
        // Version 1 made this column int, and lost the row
        Assert.Equal(3_000_000_000L, await InTargetAsync("SELECT Id FROM cases.AliasTypes;"));
        Assert.Equal("abc", await InTargetAsync("SELECT Name FROM cases.AliasTypes;"));
        Assert.Equal(new DateTime(2026, 10, 29, 12, 34, 56, 789), await InTargetAsync("SELECT Stamp FROM cases.AliasTypes;"));
    }

    [Fact]
    public async Task Lossy_types_keep_what_they_can()
    {
        Assert.Equal("<a b=\"1\"/>", await InTargetAsync("SELECT Doc FROM cases.LossyTypes;"));
        Assert.Equal("POINT (20.46 44.8)", await InTargetAsync("SELECT CAST(Place AS geography).STAsText() FROM cases.LossyTypes;"));
        Assert.Equal("LINESTRING (0 0, 1 1)", await InTargetAsync("SELECT CAST(Shape AS geometry).STAsText() FROM cases.LossyTypes;"));
        Assert.Equal("/1/2/", await InTargetAsync("SELECT CAST(Node AS hierarchyid).ToString() FROM cases.LossyTypes;"));
        Assert.Equal(8, ((byte[])(await InTargetAsync("SELECT Version FROM cases.LossyTypes;"))!).Length);
    }

    [Fact]
    public async Task Datetimeoffset_becomes_utc()
    {
        Assert.Equal(new DateTime(2026, 10, 29, 10, 0, 0), await InTargetAsync("SELECT Moment FROM cases.LossyTypes;"));
        Assert.Equal(new DateTime(2026, 10, 29, 17, 0, 0), await InTargetAsync("SELECT Moment FROM cases.AliasLossy;"));
    }

    [Fact]
    public async Task Identity_values_are_kept_and_new_ones_follow()
    {
        Assert.Equal("100,105", await InTargetAsync("SELECT STRING_AGG(CAST(Id AS varchar(10)), ',') WITHIN GROUP (ORDER BY Id) FROM cases.IdentitySeed;"));
        // Larger than .NET's decimal, so read as text
        Assert.Equal("1000000000000000000000000000000", await InTargetAsync("SELECT CAST(Id AS varchar(40)) FROM cases.BigSeed;"));

        // Rolled back, so the other tests still see the rows as copied
        Assert.Equal(106, await server.ScalarAsync(Target, """
            SET NOCOUNT ON;
            BEGIN TRANSACTION;
            INSERT cases.IdentitySeed (Name) VALUES (N'c');
            SELECT MAX(Id) FROM cases.IdentitySeed;
            ROLLBACK;
            """));
    }

    [Fact]
    public async Task A_null_stays_null_where_the_column_has_a_default()
    {
        Assert.Equal(1, await InTargetAsync("SELECT COUNT(*) FROM cases.Ready WHERE Owner IS NULL;"));
    }

    [Fact]
    public async Task Utf8_text_keeps_every_character_as_nvarchar()
    {
        Assert.Equal("Grüße", await InTargetAsync("SELECT Name FROM cases.Utf8;"));
    }

    [Fact]
    public async Task A_rowversion_key_keeps_its_value()
    {
        var source = await server.ScalarAsync(SqlServer.Cases, "SELECT V FROM cases.VersionKey;");

        Assert.Equal(source, await InTargetAsync("SELECT V FROM cases.VersionKey;"));
    }

    [Fact]
    public async Task Computed_columns_are_computed_again()
    {
        Assert.Equal(42, await InTargetAsync("SELECT Doubled FROM cases.ComputedIndexed;"));
    }

    [Fact]
    public async Task Names_that_need_brackets_are_copied()
    {
        Assert.Equal(3.50m, await InTargetAsync("SELECT [Unit]]Price] FROM cases.[Order Details];"));
    }

    [Fact]
    public async Task Foreign_keys_are_added_after_the_data()
    {
        Assert.Equal("FK_Child_Parent", await InTargetAsync("SELECT name FROM sys.foreign_keys;"));
        Assert.Equal(true, await InTargetAsync("SELECT CAST(CASE WHEN is_not_trusted = 0 THEN 1 ELSE 0 END AS bit) FROM sys.foreign_keys;"));
    }

    [Fact]
    public async Task Json_and_vector_keep_their_text()
    {
        Assert.SkipUnless(server.MajorVersion >= 17, "json and vector need SQL Server 2025");

        var vector = await server.ScalarAsync(SqlServer.Cases, "SELECT CAST(Embedding AS nvarchar(max)) FROM cases.NewTypes;");
        Assert.Equal(vector, await InTargetAsync("SELECT Embedding FROM cases.NewTypes;"));
        Assert.Equal("{\"a\":1}", await InTargetAsync("SELECT Document FROM cases.NewTypes;"));
    }

    [Fact]
    public async Task An_existing_target_is_refused()
    {
        await ConvertedAsync();

        var result = Run("convert", "--source", server.ConnectionString(SqlServer.Cases), "--target-db", Target, "--tables", "cases.Parent");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("exists already", result.Output);
    }

    [Fact]
    public void A_blocked_table_stops_it_before_anything_is_made()
    {
        var result = Run("convert", "--source", server.ConnectionString(SqlServer.Cases), "--target-db", "Never Made", "--tables", "cases.Variant");

        Assert.Equal(App.Blocked, result.ExitCode);
        Assert.Contains("Nothing done", result.Output);
    }

    [Fact]
    public async Task A_failure_names_its_step_and_leaves_the_database()
    {
        // A foreign key made WITH NOCHECK lets an orphan in, so the new key can't be added
        await server.ExecuteAsync("master", "CREATE DATABASE Orphans;");
        await server.ExecuteAsync("Orphans", """
            CREATE TABLE dbo.Parent (Id int PRIMARY KEY);
            CREATE TABLE dbo.Child (Id int PRIMARY KEY, ParentId int NULL);
            INSERT dbo.Child (Id, ParentId) VALUES (1, 99);
            ALTER TABLE dbo.Child WITH NOCHECK ADD CONSTRAINT FK_Child_Parent FOREIGN KEY (ParentId) REFERENCES dbo.Parent (Id);
            """);

        // The assessment sees the untrusted key, so convert refuses before it starts
        var refused = Run("convert", "--source", server.ConnectionString("Orphans"), "--target-db", "Orphans Converted");
        Assert.Equal(App.Blocked, refused.ExitCode);
        Assert.Contains("WITH NOCHECK", refused.Output);
        Assert.Equal(DBNull.Value, await server.ScalarAsync("master", "SELECT DB_ID(N'Orphans Converted');"));

        // Past the assessment, the step that fails is named, and the new database is left as it is
        var model = await Assessor.ReadAsync(server.ConnectionString("Orphans"), TestContext.Current.CancellationToken);
        var failure = await Assert.ThrowsAsync<ConversionException>(() => Converter.ConvertAsync(model,
            [new ObjectName("dbo", "Parent"), new ObjectName("dbo", "Child")],
            new Scripting.ScriptOptions("Orphans Converted", new HashSet<ObjectName>(), new HashSet<ObjectName>()),
            server.ConnectionString("Orphans"), server.ConnectionString("master"), 100, cancellationToken: TestContext.Current.CancellationToken));
        Assert.Equal("Adding the foreign keys", failure.Step);
        Assert.Equal(1L, await server.ScalarAsync("Orphans Converted", "SELECT COUNT_BIG(*) FROM dbo.Child;"));
    }

    [Fact]
    public void The_source_query_casts_what_is_mapped()
    {
        ColumnInfo[] columns =
        [
            new("Id", "int", "int", "sys", false, false, 4, 10, 0, IsNullable: false),
            new("At", "datetimeoffset", "datetimeoffset", "sys", false, false, 10, 34, 3, IsNullable: true),
            new("Doc", "xml", "xml", "sys", false, false, -1, 0, 0, IsNullable: true),
            new("Big", "BigId", "bigint", "dbo", true, false, 8, 19, 0, IsNullable: false),
            new("Twice", "int", "int", "sys", false, false, 4, 10, 0, IsNullable: true, IsComputed: true, ComputedDefinition: "([Id]*(2))"),
        ];
        var table = new TableInfo(new ObjectName("dbo", "T"), 0, 0, columns, [], [], []);

        Assert.Equal(
            "SELECT [Id], CAST(SWITCHOFFSET([At], '+00:00') AS datetime2(3)) AS [At], CAST([Doc] AS nvarchar(max)) AS [Doc], [Big] FROM [dbo].[T];",
            Converter.SourceQuery(table));
    }
}
