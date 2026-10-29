using System.Reflection;

using MOConverter.Cli;
using MOConverter.Rules;

using Spectre.Console.Cli.Testing;

namespace MOConverter.Tests;

public sealed class ReadmeTests
{
    private static readonly string Root = typeof(ReadmeTests).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        .Single(a => a.Key == "RepositoryRoot").Value!;

    [Fact]
    public void The_readme_lists_the_rules_as_the_catalogue_has_them()
    {
        var readme = File.ReadAllText(Path.Combine(Root, "README.md")).ReplaceLineEndings("\n");
        var start = readme.IndexOf('\n', readme.IndexOf("<!-- rules:", StringComparison.Ordinal)) + 1;
        var end = readme.IndexOf("<!-- /rules -->", StringComparison.Ordinal);

        // If this fails, paste the output of: moconverter rules --format markdown
        Assert.Equal(RulesCommand.Markdown(), readme[start..end]);
    }

    [Fact]
    public void Rules_lists_every_rule()
    {
        var app = new CommandAppTester();
        app.Configure(App.Configure);

        var result = app.Run("rules", "--format", "markdown");

        Assert.Equal(0, result.ExitCode);
        Assert.All(RuleCatalogue.All, rule => Assert.Contains($"`{rule.Id}`", result.Output));
    }

    [Fact]
    public void Every_rule_has_a_summary_that_ends_a_sentence()
    {
        Assert.All(RuleCatalogue.All, rule => Assert.EndsWith(".", rule.Summary));
    }
}
