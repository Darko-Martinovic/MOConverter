using System.ComponentModel;

using Microsoft.Data.SqlClient;

using MOConverter.Assessment;
using MOConverter.Catalog;
using MOConverter.Rules;
using MOConverter.Scripting;

using Spectre.Console;
using Spectre.Console.Cli;

namespace MOConverter.Cli;

public sealed class ScriptSettings : SelectionSettings
{
    [CommandOption("-d|--target-db <DATABASE>")]
    [Description("The database the script creates the memory-optimized tables in. The script creates it if it doesn't exist.")]
    public string? TargetDatabase { get; init; }

    [CommandOption("-o|--out <FILE>")]
    [Description("The file to write the script to.")]
    public string? Out { get; init; }

    [CommandOption("--accept-lossy")]
    [Description("Script tables whose conversion changes meaning too, each change marked LOSSY in the script. Without it, nothing is written while any table is lossy.")]
    public bool AcceptLossy { get; init; }

    [CommandOption("--hash-pk <TABLES>")]
    [Description("Comma-separated tables whose primary key becomes a hash index, sized for twice their rows. The others get a range index, as do tables without version 1's MOIndexEp = 'HASH' extended property.")]
    public string? HashPrimaryKeys { get; init; }

    [CommandOption("--schema-only <TABLES>")]
    [Description("Comma-separated tables made DURABILITY = SCHEMA_ONLY: their rows don't survive a restart.")]
    public string? SchemaOnly { get; init; }

    public override ValidationResult Validate()
    {
        if (string.IsNullOrWhiteSpace(TargetDatabase))
            return ValidationResult.Error("Name the database to create the tables in, with --target-db.");
        if (string.IsNullOrWhiteSpace(Out))
            return ValidationResult.Error("Name the file to write, with --out.");

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
        return string.Equals(source, TargetDatabase, StringComparison.OrdinalIgnoreCase)
            ? ValidationResult.Error("The target database must not be the source: the script makes new tables with the same names.")
            : ValidationResult.Success();
    }
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
        if (Refused(assessment, settings.AcceptLossy) is { } refusal)
            return refusal;

        var tables = assessment.Tables.Select(t => t.Name).ToList();
        if (tables.Count == 0)
        {
            console.MarkupLine("[yellow]Nothing to script:[/] no table is selected, or every selected one is memory-optimized already.");
            return 0;
        }

        var hash = Selection.Parse(settings.HashPrimaryKeys, null);
        var schemaOnly = Selection.Parse(settings.SchemaOnly, null);
        var unmatched = hash.Unmatched(tables).Select(n => $"--hash-pk: {n}")
            .Concat(schemaOnly.Unmatched(tables).Select(n => $"--schema-only: {n}"))
            .ToList();
        if (unmatched.Count > 0)
        {
            console.MarkupLineInterpolated($"[red]Not among the tables to script:[/] {string.Join(", ", unmatched)}");
            return 1;
        }

        var options = new ScriptOptions(settings.TargetDatabase!, hash.IsAll ? new HashSet<ObjectName>() : hash.Resolve(tables),
            schemaOnly.IsAll ? new HashSet<ObjectName>() : schemaOnly.Resolve(tables));
        await File.WriteAllTextAsync(settings.Out!, Scripter.Script(model, tables, options), cancellationToken);

        var counts = assessment.Tables.GroupBy(t => t.Outcome).OrderBy(g => g.Key).Select(g => $"{g.Count()} {g.Key.ToString().ToLowerInvariant()}");
        console.MarkupLineInterpolated($"[green]Wrote {settings.Out}:[/] {tables.Count} {(tables.Count == 1 ? "table" : "tables")} ({string.Join(", ", counts)}) in {ObjectName.Quote(settings.TargetDatabase!)}.");
        console.MarkupLine("Review it, and run it on the server. It creates empty tables: [bold]moconverter convert[/] copies the data.");
        return 0;
    }

    /// <summary>An exit code when the selection can't be scripted, after saying why; null when it can.</summary>
    private int? Refused(Assessment.Assessment assessment, bool acceptLossy)
    {
        if (assessment.Outcome == Outcome.Blocked)
        {
            Reports.Table(console, assessment);
            console.MarkupLine("[red]Nothing written:[/] something selected is blocked. Leave those tables out with --tables or --schemas, or change them.");
            return App.Blocked;
        }

        var lossy = assessment.Tables.Where(t => t.Outcome == Outcome.Lossy).ToList();
        if (lossy.Count > 0 && !acceptLossy)
        {
            console.MarkupLine("[yellow]These changes lose something:[/]");
            foreach (var table in lossy)
            {
                foreach (var finding in table.Findings.Where(f => f.Outcome == Outcome.Lossy))
                    console.MarkupLineInterpolated($"  {table.Name}: {finding.Message}");
            }

            console.MarkupLine("[red]Nothing written:[/] accept these changes with --accept-lossy, or leave those tables out.");
            return App.Blocked;
        }

        return null;
    }
}
