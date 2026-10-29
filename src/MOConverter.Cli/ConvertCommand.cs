using System.ComponentModel;

using Microsoft.Data.SqlClient;

using MOConverter.Assessment;
using MOConverter.Catalog;
using MOConverter.Conversion;

using Spectre.Console;
using Spectre.Console.Cli;

namespace MOConverter.Cli;

public sealed class ConvertSettings : ConversionSettings
{
    public const int DefaultBatchSize = 10_000;

    [CommandOption("--target <CONNECTION_STRING>")]
    [Description("The server to create the new database on, as a connection string. Without it, the source's server. Its database is ignored.")]
    public string? Target { get; init; }

    [CommandOption("--batch-size <ROWS>")]
    [Description("Rows copied in each batch, each in a transaction of its own.")]
    [DefaultValue(DefaultBatchSize)]
    public int BatchSize { get; init; } = DefaultBatchSize;

    protected override string? TargetServer => Target;

    public string TargetConnectionString()
    {
        var builder = new SqlConnectionStringBuilder(Target ?? ConnectionString());
        if (TrustServerCertificate)
            builder.TrustServerCertificate = true;
        return builder.ConnectionString;
    }

    public override ValidationResult Validate()
    {
        if (BatchSize < 1)
            return ValidationResult.Error("--batch-size must be at least 1.");
        if (Target is not null)
        {
            try
            {
                _ = new SqlConnectionStringBuilder(Target);
            }
            catch (ArgumentException e)
            {
                return ValidationResult.Error($"The --target connection string isn't valid: {e.Message}");
            }
        }

        return base.Validate();
    }
}

public sealed class ConvertCommand(IAnsiConsole console) : AsyncCommand<ConvertSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ConvertSettings settings, CancellationToken cancellationToken)
    {
        var target = settings.TargetConnectionString();
        DatabaseModel model;
        try
        {
            model = await Assessor.ReadAsync(settings.ConnectionString(), cancellationToken);
            if (await ExistsAsync(target, settings.TargetDatabase!, cancellationToken))
            {
                console.MarkupLineInterpolated($"[red]Nothing done:[/] {ObjectName.Quote(settings.TargetDatabase!)} exists already. convert makes a new database; drop it, or name another.");
                return 1;
            }
        }
        catch (SqlException e)
        {
            console.MarkupLineInterpolated($"[red]Couldn't read the database:[/] {e.Message}");
            return 1;
        }

        var assessment = Assessor.Assess(model, settings.Selection);
        if (settings.Plan(console, assessment, "Nothing done", out var exitCode) is not { } plan)
            return exitCode;

        console.MarkupLineInterpolated($"Converting {plan.Tables.Count} {(plan.Tables.Count == 1 ? "table" : "tables")} ({ConversionSettings.Counts(assessment)}) into {ObjectName.Quote(settings.TargetDatabase!)}.");
        var progress = new Reporter<TableCopied>(copied => console.MarkupLineInterpolated(
            $"  {copied.Table}: {copied.Rows.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} rows in {copied.Took.TotalSeconds.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s"));
        try
        {
            var copied = await Converter.ConvertAsync(model, plan.Tables, plan.Options, settings.ConnectionString(), target,
                settings.BatchSize, progress, cancellationToken);
            console.MarkupLineInterpolated($"[green]Done:[/] {copied.Sum(c => c.Rows).ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} rows in {plan.Tables.Count} memory-optimized tables, and their foreign keys.");
            return 0;
        }
        catch (ConversionException e)
        {
            console.MarkupLineInterpolated($"[red]{e.Step} failed:[/] {e.InnerException?.Message}");
            console.MarkupLineInterpolated($"{ObjectName.Quote(settings.TargetDatabase!)} is left as it is, for a look. Drop it before trying again.");
            return 1;
        }
    }

    private static async Task<bool> ExistsAsync(string target, string database, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(new SqlConnectionStringBuilder(target) { InitialCatalog = "master" }.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand("SELECT DB_ID(@name);", connection);
        command.Parameters.AddWithValue("@name", database);
        return await command.ExecuteScalarAsync(cancellationToken) is not DBNull and not null;
    }

    /// <summary>
    /// Reports on the calling thread, unlike Progress, so the lines come out in order and before
    /// the command returns.
    /// </summary>
    private sealed class Reporter<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
