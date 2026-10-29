using MOConverter.Assessment;
using MOConverter.Catalog;

namespace MOConverter.Tests;

public sealed class SelectionTests
{
    private static readonly ObjectName[] Tables =
    [
        new("dbo", "Orders"), new("sales", "Customers"), new("sales", "Orders"), new("hr", "People"),
    ];

    [Fact]
    public void Nothing_named_means_every_table()
    {
        var selection = Selection.Parse(null, " ");

        Assert.True(selection.IsAll);
        Assert.Equal(Tables.Length, selection.Resolve(Tables).Count);
    }

    [Fact]
    public void Tables_are_schema_qualified_or_bare()
    {
        var selection = Selection.Parse("DBO.orders, Customers", null);

        Assert.Equal([new SelectedTable("DBO", "orders"), new SelectedTable(null, "Customers")], selection.Tables);
        Assert.Equal([new ObjectName("dbo", "Orders"), new ObjectName("sales", "Customers")], selection.Resolve(Tables).Order(ByName));
    }

    [Fact]
    public void An_exact_match_wins_over_one_that_differs_in_case()
    {
        // As in a database with a case-sensitive collation
        ObjectName[] tables = [new("dbo", "Orders"), new("dbo", "orders")];

        Assert.Equal([new ObjectName("dbo", "orders")], Selection.Parse("dbo.orders", null).Resolve(tables));
        Assert.Equal(2, Selection.Parse("dbo.ORDERS", null).Resolve(tables).Count);
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
        var selection = Selection.Parse(null, "Sales,[hr]");

        Assert.Equal(
            [new ObjectName("hr", "People"), new ObjectName("sales", "Customers"), new ObjectName("sales", "Orders")],
            selection.Resolve(Tables).Order(ByName));
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

        Assert.Equal(["schema [ghost]", "table [dbo].[Nope]"], selection.Unmatched(Tables));
    }

    private static readonly Comparer<ObjectName> ByName = Comparer<ObjectName>.Create((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
}
