using Spectre.Console.Cli;

namespace MOConverter.Cli;

public static class App
{
    /// <summary>Exit codes: 0 nothing is blocked, 1 the command failed, 2 something selected is blocked.</summary>
    public const int Blocked = 2;

    public static CommandApp Create()
    {
        var app = new CommandApp();
        app.Configure(Configure);
        return app;
    }

    public static void Configure(IConfigurator config)
    {
        config.SetApplicationName("moconverter");
        config.AddCommand<AssessCommand>("assess")
            .WithDescription("Say which tables can become memory-optimized, and why the others can't. Changes nothing.")
            .WithExample("assess", "--source", "\"Server=.;Database=Sales;Integrated Security=true;TrustServerCertificate=true\"")
            .WithExample("assess", "--tables", "dbo.Orders,dbo.OrderLines", "--format", "json");
        config.AddCommand<ScriptCommand>("script")
            .WithDescription("Write the T-SQL that creates memory-optimized copies of the selected tables in another database. Changes nothing.")
            .WithExample("script", "--tables", "dbo.Orders,dbo.OrderLines", "--target-db", "SalesInMemory", "--out", "convert.sql")
            .WithExample("script", "--schemas", "sales", "--target-db", "SalesInMemory", "--out", "convert.sql", "--accept-lossy", "--hash-pk", "sales.Orders");
    }
}
