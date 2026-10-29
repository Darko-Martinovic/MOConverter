using MOConverter.Assessment;
using MOConverter.Catalog;

namespace MOConverter.Tests;

public sealed class SelectionTests
{
    [Fact]
    public void Nothing_named_means_every_table()
    {
        var selection = Selection.Parse(null, " ");

        Assert.True(selection.IsAll);
        Assert.True(selection.Includes(new ObjectName("any", "Table")));
    }

    [Fact]
    public void Tables_are_schema_qualified_or_bare()
    {
        var selection = Selection.Parse("dbo.Orders, Customers", null);

        Assert.Equal([new SelectedTable("dbo", "Orders"), new SelectedTable(null, "Customers")], selection.Tables);
        Assert.True(selection.Includes(new ObjectName("DBO", "orders")));
        Assert.True(selection.Includes(new ObjectName("sales", "Customers")));
        Assert.False(selection.Includes(new ObjectName("sales", "Orders")));
    }

    [Fact]
    public void Brackets_and_quotes_keep_dots_commas_and_spaces()
    {
        var selection = Selection.Parse("[Sales].[Order Details],\"odd,name\".[a.b],[x]]y]", null);

        Assert.Equal(
            [new SelectedTable("Sales", "Order Details"), new SelectedTable("odd,name", "a.b"), new SelectedTable(null, "x]y")],
            selection.Tables);
    }

    [Fact]
    public void Schemas_select_their_tables()
    {
        var selection = Selection.Parse(null, "sales,[hr]");

        Assert.True(selection.Includes(new ObjectName("Sales", "Orders")));
        Assert.True(selection.Includes(new ObjectName("hr", "People")));
        Assert.False(selection.Includes(new ObjectName("dbo", "Orders")));
    }

    [Theory]
    [InlineData("a.b.c", null)]
    [InlineData("[unclosed", null)]
    [InlineData(null, "sales.dbo")]
    public void Bad_names_are_refused(string? tables, string? schemas)
    {
        Assert.Throws<FormatException>(() => Selection.Parse(tables, schemas));
    }

    [Fact]
    public void Names_that_match_nothing_are_reported()
    {
        var selection = Selection.Parse("dbo.Orders,dbo.Nope", "ghost");

        Assert.Equal(["schema [ghost]", "table [dbo].[Nope]"], selection.Unmatched([new ObjectName("dbo", "Orders")]));
    }
}
