using System.ComponentModel;

using Microsoft.Data.SqlClient;

using MOConverter.Assessment;
using MOConverter.Catalog;
using MOConverter.Rules;
using MOConverter.Scripting;

using Spectre.Console;
using Spectre.Console.Cli;

namespace MOConverter.Cli;

/// <summary>What script and convert share: where the tables go, and how.</summary>
public class ConversionSettings : SelectionSettings
{
    [CommandOption("-d|--target-db <DATABASE>")]
    [Description("The database to create the memory-optimized tables in.")]
    public string? TargetDatabase { get; init; }

    [CommandOption("--accept-lossy")]
    [Description("Convert tables whose conversion changes meaning too, each change marked LOSSY in the script. Without it, nothing is done while any table is lossy.")]
    public bool AcceptLossy { get; init; }

    [CommandOption("--hash-pk <TABLES>")]
    [Description("Comma-separated tables whose primary key becomes a hash index, sized for twice their rows. The others get a range index, unless version 1's MOIndexEp extended property says HASH.")]
    public string? HashPrimaryKeys { get; init; }

    [CommandOption("--schema-only <TABLES>")]
    [Description("Comma-separated tables made DURABILITY = SCHEMA_ONLY: their rows don't survive a restart.")]
    public string? SchemaOnly { get; init; }

    /// <summary>The server the target database is on, when it isn't the source's.</summary>
    protected virtual string? TargetServer => null;

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(TargetDatabase))
            return ValidationResult.Error("Name the database to create the tables in, with --target-db.");

        try
        {
            Selection.Parse(HashPrimaryKeys, null);
            Selection.Parse(SchemaOnly, null);
        }
        catch (FormatException e)
        {
            return ValidationResult.Error(e.Message);
        }

        var result = base.Validate();
        if (!result.Successful)
            return result;

        var source = new SqlConnectionStringBuilder(ConnectionString()).InitialCatalog;
        return TargetServer is null && string.Equals(source, TargetDatabase, StringComparison.OrdinalIgnoreCase)
            ? ValidationResult.Error("The target database must not be the source: the new tables have the same names.")
            : ValidationResult.Success();
    }

    /// <summary>
    /// The tables to convert and how, after saying why not when they can't be: an exit code then.
    /// </summary>
    public (IReadOnlyList<ObjectName> Tables, ScriptOptions Options)? Plan(IAnsiConsole console, Assessment.Assessment assessment, string nothing, out int exitCode)
    {
        exitCode = 0;
        if (assessment.Outcome == Outcome.Blocked)
        {
            Reports.Table(console, assessment);
            console.MarkupLine($"[red]{nothing}:[/] something selected is blocked. Leave those tables out with --tables or --schemas, or change them.");
            exitCode = App.Blocked;
            return null;
        }

        var lossy = assessment.Tables.Where(t => t.Outcome == Outcome.Lossy).ToList();
        if (lossy.Count > 0 && !AcceptLossy)
        {
            console.MarkupLine("[yellow]These changes lose something:[/]");
            foreach (var table in lossy)
            {
                foreach (var finding in table.Findings.Where(f => f.Outcome == Outcome.Lossy))
                    console.MarkupLineInterpolated($"  {table.Name}: {finding.Message}");
            }

            console.MarkupLine($"[red]{nothing}:[/] accept these changes with --accept-lossy, or leave those tables out.");
            exitCode = App.Blocked;
            return null;
        }

        var tables = assessment.Tables.Select(t => t.Name).ToList();
        if (tables.Count == 0)
        {
            console.MarkupLine("[yellow]Nothing to do:[/] no table is selected, or every selected one is memory-optimized already.");
            return null;
        }

        var hash = Selection.Parse(HashPrimaryKeys, null);
        var schemaOnly = Selection.Parse(SchemaOnly, null);
        var unmatched = hash.Unmatched(tables).Select(n => $"--hash-pk: {n}")
            .Concat(schemaOnly.Unmatched(tables).Select(n => $"--schema-only: {n}"))
            .ToList();
        if (unmatched.Count > 0)
        {
            console.MarkupLineInterpolated($"[red]Not among the tables to convert:[/] {string.Join(", ", unmatched)}");
            exitCode = 1;
            return null;
        }

        return (tables, new ScriptOptions(TargetDatabase!,
            hash.IsAll ? new HashSet<ObjectName>() : hash.Resolve(tables),
            schemaOnly.IsAll ? new HashSet<ObjectName>() : schemaOnly.Resolve(tables)));
    }

    public static string Counts(Assessment.Assessment assessment) =>
        string.Join(", ", assessment.Tables.GroupBy(t => t.Outcome).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key.ToString().ToLowerInvariant()}"));
}

public sealed class ScriptSettings : ConversionSettings
{
    [CommandOption("-o|--out <FILE>")]
    [Description("The file to write the script to.")]
    public string? Out { get; init; }

    public override ValidationResult Validate() =>
        string.IsNullOrWhiteSpace(Out) ? ValidationResult.Error("Name the file to write, with --out.") : base.Validate();
}

public sealed class ScriptCommand(IAnsiConsole console) : AsyncCommand<ScriptSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, ScriptSettings settings, CancellationToken cancellationToken)
    {
        DatabaseModel model;
        try
        {
            model = await Assessor.ReadAsync(settings.ConnectionString(), cancellationToken);
        }
        catch (SqlException e)
        {
            console.MarkupLineInterpolated($"[red]Couldn't read the database:[/] {e.Message}");
            return 1;
        }

        var assessment = Assessor.Assess(model, settings.Selection);
        if (settings.Plan(console, assessment, "Nothing written", out var exitCode) is not { } plan)
            return exitCode;

        await File.WriteAllTextAsync(settings.Out!, Scripter.Script(model, plan.Tables, plan.Options), cancellationToken);

        console.MarkupLineInterpolated($"[green]Wrote {settings.Out}:[/] {plan.Tables.Count} {(plan.Tables.Count == 1 ? "table" : "tables")} ({ConversionSettings.Counts(assessment)}) in {ObjectName.Quote(settings.TargetDatabase!)}.");
        console.MarkupLine("Review it, and run it on the server. It creates empty tables: [bold]moconverter convert[/] copies the data.");
        return 0;
    }
}
