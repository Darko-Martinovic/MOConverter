using MOConverter.Catalog;
using MOConverter.Scripting;

namespace MOConverter.Tests;

/// <summary>The script for models built in memory.</summary>
public sealed class ScripterTests
{
    private static readonly ServerFacts Server = new(
        "17.0.5035.5", 17, "Enterprise Developer Edition (64-bit)", 3, true, null, "Sales", 170, false, null, "test",
        DatabaseCollation: "Latin1_General_100_CI_AS");

    private static readonly ObjectName Orders = new("sales", "Order]Lines");

    private static string Script(TableInfo table, bool hash = false, IReadOnlyList<ForeignKeyInfo>? keys = null) =>
        Scripter.Script(new DatabaseModel(Server, [table], keys ?? []), [table.Name],
            new ScriptOptions("Sales_InMem", hash ? new HashSet<ObjectName> { table.Name } : [], new HashSet<ObjectName>()));

    private static TableInfo Table(long rows = 0, IReadOnlyList<ColumnInfo>? columns = null, IReadOnlyList<IndexInfo>? indexes = null, string? hint = null) =>
        new(Orders, rows, 0,
            columns ?? [new ColumnInfo("Id", "int", "int", "sys", false, false, 4, 10, 0, IsNullable: false)],
            indexes ?? [new IndexInfo("PK_Orders", "CLUSTERED", true, true, false, false, null, false, [new IndexColumn("Id", true)], [])],
            [], [], PrimaryKeyHint: hint);

    [Theory]
    [InlineData(0, 64)]
    [InlineData(31, 64)]
    [InlineData(33, 128)]
    [InlineData(1000, 2048)]
    [InlineData(1024, 2048)]
    [InlineData(10_000_000_000, 1 << 30)]
    public void Buckets_are_twice_the_rows_as_a_power_of_two(long rows, int buckets)
    {
        Assert.Equal(buckets, Scripter.BucketCount(rows));
    }

    [Fact]
    public void A_range_primary_key_keeps_descending_columns()
    {
        Assert.Contains("CONSTRAINT [PK_Orders] PRIMARY KEY NONCLUSTERED ([Id] DESC)", Script(Table()));
    }

    [Fact]
    public void A_hash_primary_key_drops_the_order_and_gets_buckets()
    {
        Assert.Contains("CONSTRAINT [PK_Orders] PRIMARY KEY NONCLUSTERED HASH ([Id]) WITH (BUCKET_COUNT = 2048)", Script(Table(rows: 1000), hash: true));
    }

    [Fact]
    public void Version_1s_extended_property_asks_for_a_hash_key()
    {
        Assert.Contains("PRIMARY KEY NONCLUSTERED HASH", Script(Table(hint: "hash")));
        Assert.DoesNotContain("HASH", Script(Table(hint: "RANGE")));
    }

    [Fact]
    public void Names_are_quoted()
    {
        var script = Script(Table());

        Assert.Contains("CREATE TABLE [sales].[Order]]Lines]", script);
        Assert.Contains("IF SCHEMA_ID(N'sales') IS NULL EXEC (N'CREATE SCHEMA [sales]');", script);
    }

    [Fact]
    public void The_database_gets_the_source_collation()
    {
        Assert.Contains("CREATE DATABASE [Sales_InMem] COLLATE Latin1_General_100_CI_AS;", Script(Table()));
    }

    [Fact]
    public void Text_columns_keep_their_collation_and_changes_are_marked()
    {
        ColumnInfo[] columns =
        [
            new("Id", "int", "int", "sys", false, false, 4, 10, 0, IsNullable: false),
            new("Notes", "text", "text", "sys", false, false, 16, 0, 0, IsNullable: true, Collation: "Latin1_General_CS_AS"),
            new("At", "datetimeoffset", "datetimeoffset", "sys", false, false, 10, 34, 7, IsNullable: true),
        ];

        var script = Script(Table(columns: columns));

        Assert.Contains("""
                -- FIXED: text → varchar(max)
                [Notes] varchar(max) COLLATE Latin1_General_CS_AS NULL,
            """.ReplaceLineEndings(), script.ReplaceLineEndings());
        Assert.Contains("""
                -- LOSSY: datetimeoffset(7) → datetime2(7): it is stored in UTC, and the offset is lost
                [At] datetime2(7) NULL,
            """.ReplaceLineEndings(), script.ReplaceLineEndings());
    }

    [Fact]
    public void Unique_constraints_and_unique_indexes_stay_what_they_are()
    {
        IndexInfo[] indexes =
        [
            new("PK_Orders", "CLUSTERED", true, true, false, false, null, false, [new IndexColumn("Id", false)], []),
            new("UQ_Code", "NONCLUSTERED", false, true, true, false, null, false, [new IndexColumn("Id", false)], []),
            new("UX_Code", "NONCLUSTERED", false, true, false, false, null, false, [new IndexColumn("Id", false)], []),
            new("CX_Code", "CLUSTERED", false, false, false, true, "([Id]>(0))", false, [new IndexColumn("Id", false)], ["Id"]),
        ];

        var script = Script(Table(indexes: indexes)).ReplaceLineEndings();

        Assert.Contains("CONSTRAINT [UQ_Code] UNIQUE NONCLUSTERED ([Id])", script);
        Assert.Contains("INDEX [UX_Code] UNIQUE NONCLUSTERED ([Id])", script);
        Assert.Contains("""
                -- FIXED: clustered, and becomes nonclustered
                -- FIXED: its filter WHERE ([Id]>(0)) is dropped, and the index covers every row
                -- FIXED: INCLUDE ([Id]) is dropped: every index covers all columns
                INDEX [CX_Code] NONCLUSTERED ([Id])
            """.ReplaceLineEndings(), script);
    }

    [Fact]
    public void A_multi_line_definition_stays_inside_its_comment()
    {
        var table = Table() with { Checks = [new CheckInfo("CK_Like", "([a] like 'x%'\r\nOR [a] like 'y%')", false)] };

        Assert.Contains("-- LOSSY: check constraint [CK_Like] ([a] like 'x%' OR [a] like 'y%') uses LIKE", Script(table));
    }

    [Fact]
    public void The_filegroup_container_name_is_escaped_inside_the_dynamic_sql()
    {
        var script = Scripter.Script(new DatabaseModel(Server, [Table()], []), [Orders],
            new ScriptOptions("O'Brien", new HashSet<ObjectName>(), new HashSet<ObjectName>()));

        Assert.Contains("DECLARE @add nvarchar(max) = N'ALTER DATABASE [O''Brien] ADD FILE (NAME = N''O''''Brien_mo'', FILENAME = N'''", script);
    }
}
