using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using MOConverter.Assessment;
using MOConverter.Rules;

using Spectre.Console;

namespace MOConverter.Cli;

/// <summary>An assessment as a console table, JSON or Markdown.</summary>
public static class Reports
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static void Table(IAnsiConsole console, Assessment.Assessment assessment)
    {
        var server = assessment.Server;
        console.MarkupLineInterpolated($"[bold]Server[/]   {server.Edition}, version {server.ProductVersion}, In-Memory OLTP {(server.XtpSupported ? "supported" : "[red]not supported[/]")}");
        console.MarkupLineInterpolated($"[bold]Database[/] {server.Database} (compatibility level {server.CompatibilityLevel}){(server.HasMemoryOptimizedFilegroup ? ", has a memory-optimized filegroup" : "")}");
        console.WriteLine();

        if (assessment.Tables.Count > 0)
        {
            var table = new Table().Border(TableBorder.Rounded)
                .AddColumn("Table").AddColumn(new TableColumn("Rows").RightAligned())
                .AddColumn(new TableColumn("Memory").RightAligned()).AddColumn("Outcome").AddColumn("Why");
            foreach (var t in assessment.Tables)
            {
                table.AddRow(
                    new Text(t.Name.ToString()),
                    new Text(t.Rows.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)),
                    new Text(Assessor.Size(t.EstimatedBytes)),
                    new Markup(Colour(t.Outcome)),
                    new Text(string.Join(Environment.NewLine, t.Findings.OrderByDescending(f => f.Outcome).Select(f => $"{Word(f.Outcome)}: {f.Message}"))));
            }

            console.Write(table);
        }

        if (assessment.AlreadyMemoryOptimized.Count > 0)
            console.MarkupLineInterpolated($"Already memory-optimized, left out: {string.Join(", ", assessment.AlreadyMemoryOptimized)}");

        foreach (var note in assessment.Notes.OrderByDescending(n => n.Outcome))
            console.MarkupLine($"{(note.Outcome == Outcome.Ready ? "[grey]note[/]" : Colour(note.Outcome))} {Markup.Escape(note.Message)}");

        var counts = assessment.Tables.GroupBy(t => t.Outcome).OrderBy(g => g.Key).Select(g => $"{g.Count()} {Word(g.Key)}");
        console.MarkupLineInterpolated($"[bold]{assessment.Tables.Count} {(assessment.Tables.Count == 1 ? "table" : "tables")}:[/] {string.Join(", ", counts)}. The ones that can be converted need about {Assessor.Size(assessment.EstimatedBytes)} of memory.");
    }

    public static string Json(Assessment.Assessment assessment) =>
        JsonSerializer.Serialize(new
        {
            server = assessment.Server,
            outcome = assessment.Outcome,
            estimatedBytes = assessment.EstimatedBytes,
            notes = assessment.Notes.Select(FindingView),
            tables = assessment.Tables.Select(t => new
            {
                schema = t.Name.Schema,
                name = t.Name.Name,
                outcome = t.Outcome,
                rows = t.Rows,
                estimatedBytes = t.EstimatedBytes,
                findings = t.Findings.Select(FindingView),
            }),
            alreadyMemoryOptimized = assessment.AlreadyMemoryOptimized.Select(n => n.ToString()),
        }, JsonOptions);

    public static string Markdown(Assessment.Assessment assessment)
    {
        var server = assessment.Server;
        var text = new StringBuilder();
        text.AppendLine($"# In-Memory OLTP assessment of {server.Database}");
        text.AppendLine();
        text.AppendLine($"{server.Edition}, version {server.ProductVersion}, compatibility level {server.CompatibilityLevel}. Outcome: **{Word(assessment.Outcome)}**.");
        text.AppendLine();
        text.AppendLine("| Table | Rows | Memory | Outcome | Why |");
        text.AppendLine("|---|--:|--:|---|---|");
        foreach (var t in assessment.Tables)
        {
            var why = string.Join("<br>", t.Findings.OrderByDescending(f => f.Outcome).Select(f => Cell($"{Word(f.Outcome)}: {f.Message}")));
            text.AppendLine($"| {Cell(t.Name.ToString())} | {t.Rows.ToString("N0", System.Globalization.CultureInfo.InvariantCulture)} | {Assessor.Size(t.EstimatedBytes)} | {Word(t.Outcome)} | {why} |");
        }

        text.AppendLine();
        foreach (var note in assessment.Notes.OrderByDescending(n => n.Outcome))
            text.AppendLine($"- {(note.Outcome == Outcome.Ready ? "note" : Word(note.Outcome))}: {note.Message}");
        if (assessment.AlreadyMemoryOptimized.Count > 0)
            text.AppendLine($"- Already memory-optimized, left out: {string.Join(", ", assessment.AlreadyMemoryOptimized)}");
        return text.ToString();

        // A definition can span lines, which would end the table row
        static string Cell(string value) => value.Replace("|", "\\|").Replace("\r\n", "<br>").Replace('\r', '\n').Replace("\n", "<br>");
    }

    private static object FindingView(Finding finding) =>
        new { rule = finding.Rule.Id, outcome = finding.Outcome, message = finding.Message };

    private static string Word(Outcome outcome) => outcome.ToString().ToLowerInvariant();

    private static string Colour(Outcome outcome) => outcome switch
    {
        Outcome.Ready => "[green]ready[/]",
        Outcome.Fixed => "[blue]fixed[/]",
        Outcome.Lossy => "[yellow]lossy[/]",
        _ => "[red]blocked[/]",
    };
}
