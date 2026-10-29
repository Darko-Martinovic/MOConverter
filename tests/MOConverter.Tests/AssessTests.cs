using MOConverter.Assessment;
using MOConverter.Catalog;
using MOConverter.Rules;

namespace MOConverter.Tests;

/// <summary>The assessment of each table in Fixtures/cases.sql, read from a real server.</summary>
[Collection(SqlServerCollection.Name)]
public sealed class AssessTests(SqlServer server)
{
    private Task<Assessment.Assessment> AssessAsync(Selection? selection = null) =>
        Assessor.AssessAsync(server.ConnectionString(SqlServer.Cases), selection ?? Selection.Parse(null, "cases"), TestContext.Current.CancellationToken);

    [Theory]
    [InlineData("Ready", Outcome.Ready)]
    [InlineData("UpperLimit", Outcome.Ready)]
    [InlineData("ManyIndexes", Outcome.Ready)]
    [InlineData("Columnstore", Outcome.Ready)]
    [InlineData("Order Details", Outcome.Ready)]
    [InlineData("Parent", Outcome.Ready)]
    [InlineData("AliasTypes", Outcome.Fixed, "alias-type", "alias-type", "alias-type")]
    [InlineData("OldLobTypes", Outcome.Fixed, "old-lob-type", "old-lob-type", "old-lob-type")]
    [InlineData("Indexes", Outcome.Fixed, "clustered-index", "included-columns", "filtered-index", "nonclustered-columnstore")]
    [InlineData("ComputedIndexed", Outcome.Fixed, "computed-index-persisted")]
    [InlineData("Sparse", Outcome.Fixed, "sparse")]
    [InlineData("Partitioned", Outcome.Fixed, "partitioned")]
    [InlineData("Compressed", Outcome.Fixed, "compression")]
    [InlineData("Disabled", Outcome.Fixed, "disabled", "disabled")]
    [InlineData("LossyTypes", Outcome.Lossy, "type-lossy", "type-lossy", "type-lossy", "type-lossy", "type-lossy", "type-lossy")]
    [InlineData("AliasLossy", Outcome.Lossy, "type-lossy")]
    [InlineData("Checks", Outcome.Lossy, "check-unsupported", "check-unsupported")]
    [InlineData("Defaults", Outcome.Lossy, "default-unsupported", "default-unsupported")]
    [InlineData("IdentitySeed", Outcome.Lossy, "identity-seed")]
    [InlineData("BigSeed", Outcome.Lossy, "identity-seed")]
    [InlineData("Child", Outcome.Lossy, "foreign-key-action")]
    [InlineData("Audited", Outcome.Lossy, "trigger")]
    [InlineData("Tracked", Outcome.Lossy, "change-tracking")]
    [InlineData("NoPrimaryKey", Outcome.Blocked, "no-primary-key")]
    [InlineData("Variant", Outcome.Blocked, "type-unsupported")]
    [InlineData("ComputedUpper", Outcome.Blocked, "computed-unsupported")]
    [InlineData("ComputedVariant", Outcome.Blocked, "computed-unsupported")]
    [InlineData("UniqueChild", Outcome.Blocked, "foreign-key-target")]
    [InlineData("UniqueParent", Outcome.Blocked, "foreign-key-boundary")]
    [InlineData("UniqueFiltered", Outcome.Blocked, "unique-filtered-index")]
    [InlineData("ColumnSet", Outcome.Blocked, "column-set", "sparse")]
    [InlineData("Temporal", Outcome.Blocked, "temporal")]
    // SQL Server gives a history table a clustered index and page compression of its own
    [InlineData("TemporalHistory", Outcome.Blocked, "temporal", "no-primary-key", "clustered-index", "compression")]
    [InlineData("Linked", Outcome.Blocked, "foreign-key-boundary")]
    [InlineData("LinkedToLinked", Outcome.Blocked, "foreign-key-boundary")]
    public async Task Each_case_has_its_outcome_and_findings(string table, Outcome outcome, params string[] rules)
    {
        var assessed = Table(await AssessAsync(), table);

        Assert.Equal(outcome, assessed.Outcome);
        Assert.Equal(rules.Order(), assessed.Findings.Select(f => f.Rule.Id).Order());
    }

    [Fact]
    public async Task Graph_tables_are_blocked()
    {
        var assessed = Table(await AssessAsync(), "Person");

        Assert.Equal(Outcome.Blocked, assessed.Outcome);
        Assert.Contains(assessed.Findings, f => f.Rule == RuleCatalogue.Graph);
    }

    [Fact]
    public async Task Ledger_tables_are_blocked()
    {
        Assert.SkipUnless(server.MajorVersion >= 16, "Ledger tables need SQL Server 2022");

        var assessed = Table(await AssessAsync(), "Ledger");

        Assert.Equal(Outcome.Blocked, assessed.Outcome);
        Assert.Contains(assessed.Findings, f => f.Rule == RuleCatalogue.Ledger);
    }

    [Fact]
    public async Task Json_and_vector_are_lossy()
    {
        Assert.SkipUnless(server.MajorVersion >= 17, "json and vector need SQL Server 2025");

        var assessed = Table(await AssessAsync(), "NewTypes");

        Assert.Equal(
            ["[Document] json → nvarchar(max): the json type's validation is lost",
             "[Embedding] vector(3) → nvarchar(max): it is stored as a JSON array: CAST it back to vector(3) to use vector functions"],
            assessed.Findings.Select(f => f.Message));
    }

    [Fact]
    public async Task Alias_types_get_their_exact_base_types()
    {
        var assessed = Table(await AssessAsync(), "AliasTypes");

        Assert.Equal(
            ["[Id] dbo.BigId → bigint: the alias type's base type is bigint",
             "[Name] dbo.Name50 → varchar(50): the alias type's base type is varchar(50)",
             "[Stamp] dbo.Stamp → datetime2(3): the alias type's base type is datetime2(3)"],
            assessed.Findings.Select(f => f.Message));
    }

    [Fact]
    public async Task Findings_name_what_is_unsupported()
    {
        var assessment = await AssessAsync();

        Assert.Equal(
            ["check constraint [CK_Checks_Code] ([Code] like 'A%') uses LIKE, which memory-optimized tables don't accept, and is left out",
             "check constraint [CK_Checks_Name] (upper([Name])=[Name]) uses UPPER, which memory-optimized tables don't accept, and is left out"],
            Table(assessment, "Checks").Findings.Select(f => f.Message));
        Assert.Equal(
            "foreign key [FK_Linked_Variant] links it to [cases].[Variant], which can't be converted",
            Assert.Single(Table(assessment, "Linked").Findings).Message);
        Assert.Equal(
            "foreign key [FK_Child_Parent] ON DELETE CASCADE ON UPDATE SET NULL becomes NO ACTION",
            Assert.Single(Table(assessment, "Child").Findings).Message);
    }

    [Fact]
    public async Task A_table_linked_to_one_outside_the_selection_is_blocked()
    {
        var assessment = await AssessAsync(Selection.Parse("cases.Child", null));

        var child = Assert.Single(assessment.Tables);
        Assert.Equal(Outcome.Blocked, child.Outcome);
        Assert.Contains(child.Findings, f => f.Rule == RuleCatalogue.ForeignKeyBoundary && f.Message.EndsWith("[cases].[Parent], which isn't selected"));
    }

    [Fact]
    public async Task Selecting_both_ends_of_a_key_converts_both()
    {
        var assessment = await AssessAsync(Selection.Parse("cases.Child, [cases].[Parent]", null));

        Assert.Equal([Outcome.Lossy, Outcome.Ready], assessment.Tables.OrderBy(t => t.Name.Name).Select(t => t.Outcome));
    }

    [Fact]
    public async Task A_selected_table_that_doesnt_exist_blocks_the_assessment()
    {
        var assessment = await AssessAsync(Selection.Parse("cases.Ready,cases.Nope", null));

        Assert.Equal(Outcome.Blocked, assessment.Outcome);
        Assert.Contains(assessment.Notes, n => n.Rule == RuleCatalogue.SelectionUnmatched && n.Message == "no table [cases].[Nope] in [Cases]");
    }

    [Fact]
    public async Task The_server_is_described()
    {
        var assessment = await AssessAsync();

        Assert.Equal(server.MajorVersion, assessment.Server.MajorVersion);
        Assert.True(assessment.Server.XtpSupported);
        Assert.False(assessment.Server.HasMemoryOptimizedFilegroup);
        Assert.NotNull(assessment.Server.MemoryLimitBytes);
        Assert.DoesNotContain(assessment.Notes, n => n.Outcome == Outcome.Blocked);
    }

    [Fact]
    public async Task Rows_and_memory_are_estimated()
    {
        var ready = Table(await AssessAsync(), "Ready");

        Assert.Equal(1001, ready.Rows);
        Assert.InRange(ready.EstimatedBytes, 100_000, 10_000_000);
    }

    [Fact]
    public async Task Names_that_differ_only_in_case_are_different_tables()
    {
        var assessment = await Assessor.AssessAsync(server.ConnectionString(SqlServer.CaseSensitive), Selection.All, TestContext.Current.CancellationToken);

        Assert.Equal(
            [("Orders", Outcome.Blocked), ("orders", Outcome.Blocked)],
            assessment.Tables.Select(t => (t.Name.Name, t.Outcome)).OrderBy(t => t.Name, StringComparer.Ordinal));
        Assert.Contains(assessment.Tables.Single(t => t.Name.Name == "Orders").Findings,
            f => f.Message == "foreign key [FK_orders_Orders] links it to [dbo].[orders], which can't be converted");

        var exact = await Assessor.AssessAsync(server.ConnectionString(SqlServer.CaseSensitive), Selection.Parse("dbo.orders", null), TestContext.Current.CancellationToken);
        Assert.Equal([new ObjectName("dbo", "orders")], exact.Tables.Select(t => t.Name));
    }

    [Fact]
    public async Task Memory_optimized_tables_are_left_out()
    {
        await server.ExecuteAsync(SqlServer.Probe, "CREATE TABLE dbo.AlreadyThere (Id int NOT NULL PRIMARY KEY NONCLUSTERED) WITH (MEMORY_OPTIMIZED = ON);");

        var assessment = await Assessor.AssessAsync(server.ConnectionString(SqlServer.Probe), Selection.Parse("dbo.AlreadyThere", null), TestContext.Current.CancellationToken);

        Assert.Empty(assessment.Tables);
        Assert.Equal([new ObjectName("dbo", "AlreadyThere")], assessment.AlreadyMemoryOptimized);
        Assert.True(assessment.Server.HasMemoryOptimizedFilegroup);
    }

    private static TableAssessment Table(Assessment.Assessment assessment, string name) =>
        assessment.Tables.Single(t => t.Name == new ObjectName("cases", name));
}
