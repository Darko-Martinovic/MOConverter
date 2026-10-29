using System.Text.Json;

using MOConverter.Cli;

using Spectre.Console.Cli.Testing;

namespace MOConverter.Tests;

[Collection(SqlServerCollection.Name)]
public sealed class CliTests(SqlServer server)
{
    private static CommandAppResult Run(params string[] args)
    {
        var app = new CommandAppTester();
        app.Configure(App.Configure);
        return app.Run(args);
    }

    private string Source => server.ConnectionString(SqlServer.Cases);

    [Fact]
    public void A_blocked_table_makes_the_exit_code_2()
    {
        var result = Run("assess", "--source", Source, "--schemas", "cases");

        Assert.Equal(App.Blocked, result.ExitCode);
        Assert.Contains("NoPrimaryKey", result.Output);
        Assert.Contains("it has no primary key", result.Output);
    }

    [Fact]
    public void Nothing_blocked_makes_the_exit_code_0()
    {
        var result = Run("assess", "--source", Source, "--tables", "cases.Ready,cases.LossyTypes");

        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void Json_lists_each_table_with_its_findings()
    {
        var result = Run("assess", "--source", Source, "--tables", "cases.Checks,[cases].[Order Details]", "--format", "json");

        using var json = JsonDocument.Parse(result.Output);
        var root = json.RootElement;
        Assert.Equal("lossy", root.GetProperty("outcome").GetString());
        var tables = root.GetProperty("tables").EnumerateArray().ToList();
        Assert.Equal(["Checks", "Order Details"], tables.Select(t => t.GetProperty("name").GetString()).Order());
        var checks = tables.Single(t => t.GetProperty("name").GetString() == "Checks");
        Assert.Equal(["check-unsupported", "check-unsupported"], checks.GetProperty("findings").EnumerateArray().Select(f => f.GetProperty("rule").GetString()));
        Assert.Equal(server.MajorVersion, root.GetProperty("server").GetProperty("majorVersion").GetInt32());
    }

    [Fact]
    public void Markdown_is_a_table()
    {
        var result = Run("assess", "--source", Source, "--tables", "cases.Variant", "--format", "markdown");

        Assert.Contains("| [cases].[Variant] | 0 | 0 bytes | blocked | blocked: [Value] sql_variant: sql_variant keeps a base type per value, and no memory-optimized type can |", result.Output);
    }

    [Fact]
    public void The_source_can_come_from_the_environment()
    {
        Environment.SetEnvironmentVariable(SourceSettings.SourceVariable, Source);
        try
        {
            Assert.Equal(0, Run("assess", "--tables", "cases.Ready").ExitCode);
        }
        finally
        {
            Environment.SetEnvironmentVariable(SourceSettings.SourceVariable, null);
        }
    }

    [Fact]
    public void A_server_that_cant_be_reached_makes_the_exit_code_1()
    {
        var result = Run("assess", "--source", "Server=127.0.0.1,1;Database=x;Connect Timeout=1;TrustServerCertificate=true");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Couldn't read the database", result.Output);
    }

    [Fact]
    public void Script_writes_nothing_while_a_table_is_blocked()
    {
        var file = TempFile();

        var result = Run("script", "--source", Source, "--tables", "cases.Ready,cases.Variant", "--target-db", "Elsewhere", "--out", file);

        Assert.Equal(App.Blocked, result.ExitCode);
        Assert.Contains("Nothing written", result.Output);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public void Script_writes_lossy_tables_only_when_accepted()
    {
        var file = TempFile();
        string[] args = ["script", "--source", Source, "--tables", "cases.Ready,cases.Checks", "--target-db", "Elsewhere", "--out", file];

        var refused = Run(args);
        Assert.Equal(App.Blocked, refused.ExitCode);
        Assert.Contains("uses LIKE", refused.Output);
        Assert.False(File.Exists(file));

        var accepted = Run([.. args, "--accept-lossy", "--hash-pk", "cases.Ready"]);
        Assert.Equal(0, accepted.ExitCode);
        var script = File.ReadAllText(file);
        Assert.Contains("CREATE TABLE [cases].[Checks]", script);
        Assert.Contains("-- LOSSY: check constraint [CK_Checks_Code]", script);
        Assert.Contains("PRIMARY KEY NONCLUSTERED HASH", script);
        File.Delete(file);
    }

    [Fact]
    public void Script_refuses_options_for_tables_it_isnt_scripting()
    {
        var result = Run("script", "--source", Source, "--tables", "cases.Ready", "--target-db", "Elsewhere", "--out", TempFile(), "--schema-only", "cases.Parent");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("--schema-only: table [cases].[Parent]", result.Output);
    }

    [Fact]
    public void A_durable_table_referencing_a_schema_only_one_is_refused()
    {
        var result = Run("script", "--source", Source, "--tables", "cases.Parent,cases.Child", "--target-db", "Elsewhere",
            "--out", TempFile(), "--accept-lossy", "--schema-only", "cases.Parent");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("A durable table can't reference a SCHEMA_ONLY one", result.Output);
    }

    [Fact]
    public void A_file_that_cant_be_written_is_an_error()
    {
        var result = Run("script", "--source", Source, "--tables", "cases.Ready", "--target-db", "Elsewhere",
            "--out", Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "no-such-folder", "x.sql"));

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("Couldn't write", result.Output);
    }

    [Theory]
    [InlineData("Name the database to create the tables in", "--out", "x.sql")]
    [InlineData("can't have", "--target-db", "a/b", "--out", "x.sql")]
    [InlineData("Name the file to write", "--target-db", "x")]
    [InlineData("must not be the source", "--target-db", "cases", "--out", "x.sql")]
    public void Script_arguments_are_checked(string message, params string[] args)
    {
        var result = Run(["script", "--source", Source, .. args]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(message, result.Output);
    }

    private static string TempFile() => Path.Combine(Path.GetTempPath(), $"moconverter-{Guid.NewGuid():N}.sql");

    [Theory]
    [InlineData("Give the source database")]
    [InlineData("must name the database", "--source", "Server=.")]
    [InlineData("isn't a table name", "--source", "Server=.;Database=x", "--tables", "a.b.c")]
    public void Bad_arguments_are_explained(string message, params string[] args)
    {
        var result = Run(["assess", .. args]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(message, result.Output);
    }
}
