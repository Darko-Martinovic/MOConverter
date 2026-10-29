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
