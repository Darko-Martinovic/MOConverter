using System.ComponentModel;
using System.Text;

using MOConverter.Rules;

using Spectre.Console;
using Spectre.Console.Cli;

namespace MOConverter.Cli;

public sealed class RulesSettings : CommandSettings
{
    [CommandOption("-f|--format <FORMAT>")]
    [Description("table (the default) or markdown. json isn't offered: assess --format json names each finding's rule.")]
    [DefaultValue(OutputFormat.Table)]
    public OutputFormat Format { get; init; } = OutputFormat.Table;

    public override ValidationResult Validate() =>
        Format == OutputFormat.Json ? ValidationResult.Error("rules shows a table or markdown.") : ValidationResult.Success();
}

/// <summary>Every rule in the catalogue, worst outcome first. The README's rule table is this command's markdown.</summary>
public sealed class RulesCommand(IAnsiConsole console) : Command<RulesSettings>
{
    public override int Execute(CommandContext context, RulesSettings settings, CancellationToken cancellationToken)
    {
        if (settings.Format == OutputFormat.Markdown)
        {
            console.Profile.Width = int.MaxValue;
            console.Write(new Text(Markdown()));
            return 0;
        }

        var table = new Table().Border(TableBorder.Rounded).AddColumn("Rule").AddColumn("Outcome").AddColumn("What it means");
        foreach (var rule in Ordered())
            table.AddRow(new Text(rule.Id), new Text(rule.Outcome.ToString().ToLowerInvariant()), new Text(rule.Summary));
        console.Write(table);
        return 0;
    }

    public static string Markdown()
    {
        var text = new StringBuilder();
        text.Append("| Rule | Outcome | What it means |\n");
        text.Append("|---|---|---|\n");
        foreach (var rule in Ordered())
            text.Append($"| `{rule.Id}` | {rule.Outcome.ToString().ToLowerInvariant()} | {rule.Summary.Replace("|", "\\|")} |\n");
        return text.ToString();
    }

    private static IEnumerable<Rules.Rule> Ordered() => RuleCatalogue.All.OrderByDescending(r => r.Outcome);
}
