using MOConverter.Assessment;
using MOConverter.Catalog;
using MOConverter.Cli;
using MOConverter.Rules;

namespace MOConverter.Tests;

public sealed class ReportsTests
{
    private static readonly ServerFacts Server = new(
        "17.0.5035.5", 17, "Enterprise Developer Edition (64-bit)", 3, true, null, "Sales", 170, false, null, "not checked");

    [Fact]
    public void A_definition_over_several_lines_stays_in_its_markdown_row()
    {
        var finding = new Finding(RuleCatalogue.CheckUnsupported, "check constraint [CK] ([a] like 'x%'\r\nOR [b] like 'y%'\nOR [c] = 1 | 2) uses LIKE");
        var assessment = new Assessment.Assessment(Server, [],
            [new TableAssessment(new ObjectName("dbo", "T"), 1, 1024, [finding])], []);

        var row = Reports.Markdown(assessment).Split('\n').Single(l => l.StartsWith("| [dbo].[T]"));

        Assert.Equal(
            "| [dbo].[T] | 1 | 1 KB | lossy | lossy: check constraint [CK] ([a] like 'x%'<br>OR [b] like 'y%'<br>OR [c] = 1 \\| 2) uses LIKE |",
            row.TrimEnd('\r'));
    }
}
