using System.ComponentModel;

using Microsoft.Data.SqlClient;

using MOConverter.Assessment;
using MOConverter.Rules;

using Spectre.Console;
using Spectre.Console.Cli;

namespace MOConverter.Cli;

public enum OutputFormat
{
    Table,
    Json,
    Markdown,
}

public class SourceSettings : CommandSettings
{
    public const string SourceVariable = "MOCONVERTER_SOURCE";

    [CommandOption("-s|--source <CONNECTION_STRING>")]
    [Description($"The database to read, as a SqlClient connection string. Without it, the {SourceVariable} environment variable, which keeps a password off the command line.")]
    public string? Source { get; init; }

    [CommandOption("--trust-server-certificate")]
    [Description("Accept the server's certificate without checking it, as a container's or a test server's self-signed one.")]
    public bool TrustServerCertificate { get; init; }

    public string ConnectionString()
    {
        var source = Source ?? Environment.GetEnvironmentVariable(SourceVariable);
        var builder = new SqlConnectionStringBuilder(source);
        // Name the tool in sys.dm_exec_sessions, unless the connection string names something else
        if (builder.ApplicationName == new SqlConnectionStringBuilder().ApplicationName)
            builder.ApplicationName = "MOConverter";
        if (TrustServerCertificate)
            builder.TrustServerCertificate = true;
        return builder.ConnectionString;
    }

    public override ValidationResult Validate()
    {
        var source = Source ?? Environment.GetEnvironmentVariable(SourceVariable);
        if (string.IsNullOrWhiteSpace(source))
            return ValidationResult.Error($"Give the source database with --source, or in {SourceVariable}.");

        try
        {
            var builder = new SqlConnectionStringBuilder(source);
            if (string.IsNullOrEmpty(builder.InitialCatalog))
                return ValidationResult.Error("The connection string must name the database: Database=<name>.");
        }
        catch (ArgumentException e)
        {
            return ValidationResult.Error($"The connection string isn't valid: {e.Message}");
        }

        return ValidationResult.Success();
    }
}

public sealed class AssessSettings : SourceSettings
{
    [CommandOption("-t|--tables <TABLES>")]
    [Description("Comma-separated tables to look at: schema.table, or table in any schema. Names with commas or dots go in [[brackets]].")]
    public string? Tables { get; init; }

    [CommandOption("--schemas <SCHEMAS>")]
    [Description("Comma-separated schemas whose tables to look at. With neither --tables nor --schemas, every table.")]
    public string? Schemas { get; init; }

    [CommandOption("-f|--format <FORMAT>")]
    [Description("table (the default), json or markdown.")]
    [DefaultValue(OutputFormat.Table)]
    public OutputFormat Format { get; init; } = OutputFormat.Table;

    public override ValidationResult Validate()
    {
        try
        {
            Selection.Parse(Tables, Schemas);
        }
        catch (FormatException e)
        {
            return ValidationResult.Error(e.Message);
        }

        return base.Validate();
    }
}

public sealed class AssessCommand(IAnsiConsole console) : AsyncCommand<AssessSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, AssessSettings settings, CancellationToken cancellationToken)
    {
        Assessment.Assessment assessment;
        try
        {
            assessment = await Assessor.AssessAsync(settings.ConnectionString(), Selection.Parse(settings.Tables, settings.Schemas), cancellationToken);
        }
        catch (SqlException e)
        {
            console.MarkupLineInterpolated($"[red]Couldn't read the database:[/] {e.Message}");
            return 1;
        }

        switch (settings.Format)
        {
            case OutputFormat.Json:
                console.Profile.Width = int.MaxValue;
                console.WriteLine(Reports.Json(assessment));
                break;
            case OutputFormat.Markdown:
                console.Profile.Width = int.MaxValue;
                console.WriteLine(Reports.Markdown(assessment));
                break;
            default:
                Reports.Table(console, assessment);
                break;
        }

        return assessment.Outcome == Outcome.Blocked ? App.Blocked : 0;
    }
}
