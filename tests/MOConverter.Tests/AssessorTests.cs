using MOConverter.Assessment;
using MOConverter.Catalog;
using MOConverter.Rules;

namespace MOConverter.Tests;

/// <summary>The assessment of models built in memory, for what a test server can't easily show.</summary>
public sealed class AssessorTests
{
    private static readonly ServerFacts Server = new(
        "17.0.5035.5", 17, "Enterprise Developer Edition (64-bit)", 3, XtpSupported: true, ServiceObjective: null,
        "Sales", 170, HasMemoryOptimizedFilegroup: false, MemoryLimitBytes: 1L << 30, "test");

    private static TableInfo Table(string name, long rows = 10, long dataBytes = 8192, bool primaryKey = true,
        bool fullText = false, bool cdc = false, bool memoryOptimized = false, IReadOnlyList<IndexInfo>? indexes = null,
        IReadOnlyList<ColumnInfo>? columns = null) =>
        new(new ObjectName("dbo", name), rows, dataBytes,
            columns ?? [Id],
            indexes ?? (primaryKey ? [PrimaryKey(name)] : []),
            [], [], memoryOptimized, HasFullTextIndex: fullText, IsTrackedByCdc: cdc);

    private static readonly ColumnInfo Id = new("Id", "int", "int", "sys", false, false, 4, 10, 0, IsNullable: false);

    private static IndexInfo PrimaryKey(string table, bool disabled = false) =>
        new($"PK_{table}", "CLUSTERED", true, true, false, false, null, disabled, [new IndexColumn("Id", false)], []);

    private static ForeignKeyInfo Key(string from, string to) =>
        new($"FK_{from}_{to}", new ObjectName("dbo", from), new ObjectName("dbo", to), "NO_ACTION", "NO_ACTION", false, true, [("Id", "Id")]);

    private static Assessment.Assessment Assess(IReadOnlyList<TableInfo> tables, IReadOnlyList<ForeignKeyInfo>? keys = null,
        ServerFacts? server = null, Selection? selection = null) =>
        Assessor.Assess(new DatabaseModel(server ?? Server, tables, keys ?? []), selection ?? Selection.All);

    private static Outcome OutcomeOf(Assessment.Assessment assessment, string table) =>
        assessment.Tables.Single(t => t.Name.Name == table).Outcome;

    [Fact]
    public void Blocking_spreads_along_foreign_keys_both_ways()
    {
        var assessment = Assess(
            [Table("A"), Table("B", primaryKey: false), Table("C"), Table("D"), Table("E")],
            [Key("A", "B"), Key("C", "A"), Key("D", "E")]);

        Assert.Equal(Outcome.Blocked, OutcomeOf(assessment, "A"));
        Assert.Equal(Outcome.Blocked, OutcomeOf(assessment, "C"));
        Assert.Equal(Outcome.Ready, OutcomeOf(assessment, "D"));
        Assert.Equal(Outcome.Ready, OutcomeOf(assessment, "E"));
    }

    [Fact]
    public void A_key_to_a_memory_optimized_table_blocks_it_since_that_table_isnt_copied()
    {
        var assessment = Assess([Table("A"), Table("M", memoryOptimized: true)], [Key("A", "M")]);

        Assert.Equal(Outcome.Blocked, OutcomeOf(assessment, "A"));
        Assert.Contains(assessment.Tables.Single().Findings, f => f.Message.EndsWith("which is memory-optimized already, and isn't copied into the new database"));
        Assert.Equal([new ObjectName("dbo", "M")], assessment.AlreadyMemoryOptimized);
    }

    [Fact]
    public void A_table_bigger_than_memory_is_blocked()
    {
        var assessment = Assess([Table("Big", rows: 10_000_000, dataBytes: 1L << 30), Table("Small")]);

        Assert.Equal(Outcome.Blocked, OutcomeOf(assessment, "Big"));
        Assert.Equal(Outcome.Ready, OutcomeOf(assessment, "Small"));
        Assert.DoesNotContain(assessment.Notes, n => n.Rule == RuleCatalogue.MemoryTotal);
    }

    [Fact]
    public void Tables_that_fit_alone_but_not_together_block_the_selection()
    {
        var assessment = Assess([Table("A", dataBytes: 300L << 20), Table("B", dataBytes: 300L << 20)]);

        Assert.All(assessment.Tables, t => Assert.Equal(Outcome.Ready, t.Outcome));
        Assert.Contains(assessment.Notes, n => n.Rule == RuleCatalogue.MemoryTotal);
        Assert.Equal(Outcome.Blocked, assessment.Outcome);
    }

    [Fact]
    public void The_estimate_doubles_data_and_row_overhead()
    {
        var assessment = Assess([Table("A", rows: 1000, dataBytes: 80_000)]);

        // 2 × (80,000 bytes + 1,000 rows × (24-byte header + 8 bytes for one index))
        Assert.Equal(224_000, assessment.Tables.Single().EstimatedBytes);
    }

    [Fact]
    public void Full_text_and_cdc_are_lossy()
    {
        var assessment = Assess([Table("A", fullText: true), Table("B", cdc: true)]);

        Assert.Equal(Outcome.Lossy, OutcomeOf(assessment, "A"));
        Assert.Equal(Outcome.Lossy, OutcomeOf(assessment, "B"));
    }

    [Fact]
    public void An_old_server_is_blocked()
    {
        var assessment = Assess([Table("A")], server: Server with { ProductVersion = "14.0.3456.2", MajorVersion = 14 });

        Assert.Contains(assessment.Notes, n => n.Rule == RuleCatalogue.ServerVersion && n.Message == "this is SQL Server 14.0.3456.2");
        Assert.Equal(Outcome.Blocked, assessment.Outcome);
    }

    [Fact]
    public void Azure_general_purpose_is_blocked()
    {
        var server = Server with { EngineEdition = 5, MajorVersion = 12, XtpSupported = false, ServiceObjective = "GP_Gen5_2", MemoryLimitBytes = null };

        var assessment = Assess([Table("A")], server: server);

        Assert.Contains(assessment.Notes, n => n.Rule == RuleCatalogue.ServerXtp && n.Message.Contains("GP_Gen5_2"));
        Assert.DoesNotContain(assessment.Notes, n => n.Rule == RuleCatalogue.ServerVersion);
    }

    [Fact]
    public void Standard_edition_gets_a_note_about_its_quota()
    {
        var assessment = Assess([Table("A")], server: Server with { Edition = "Standard Edition (64-bit)", EngineEdition = 2 });

        Assert.Contains(assessment.Notes, n => n.Rule == RuleCatalogue.EditionQuota);
        Assert.Equal(Outcome.Ready, assessment.Outcome);
    }

    [Fact]
    public void Without_a_memory_limit_nothing_is_blocked_for_memory()
    {
        var assessment = Assess([Table("Big", dataBytes: 1L << 40)], server: Server with { MemoryLimitBytes = null, MemoryLimitSource = "not checked" });

        Assert.Equal(Outcome.Ready, assessment.Outcome);
        Assert.Contains(assessment.Notes, n => n.Rule == RuleCatalogue.MemoryNote && n.Message == "memory not checked");
    }

    [Fact]
    public void A_disabled_primary_key_blocks_the_table()
    {
        var assessment = Assess([Table("A", indexes: [PrimaryKey("A", disabled: true)])]);

        var table = assessment.Tables.Single();
        Assert.Equal(Outcome.Blocked, table.Outcome);
        Assert.Contains(table.Findings, f => f.Rule == RuleCatalogue.NoPrimaryKey && f.Message == "its primary key [PK_A] is disabled");
    }

    [Fact]
    public void A_filter_the_login_cant_see_blocks_the_table()
    {
        // filter_definition is null both without a filter and when it's hidden; has_filter tells them apart
        var hidden = new IndexInfo("IX_A", "NONCLUSTERED", false, true, false, true, null, false, [new IndexColumn("Id", false)], []);

        var table = Assess([Table("A", indexes: [PrimaryKey("A"), hidden])]).Tables.Single();

        Assert.Equal(Outcome.Blocked, table.Outcome);
        Assert.Contains(table.Findings, f => f.Rule == RuleCatalogue.DefinitionHidden && f.Message == "index [IX_A]'s filter isn't visible");
    }

    [Fact]
    public void A_default_the_login_cant_see_blocks_the_table()
    {
        var column = Id with { Name = "Created", DefaultName = "DF_Created", DefaultDefinition = null };

        var table = Assess([Table("A", columns: [Id, column])]).Tables.Single();

        Assert.Contains(table.Findings, f => f.Rule == RuleCatalogue.DefinitionHidden && f.Message == "[Created]'s default [DF_Created] isn't visible");
    }

    [Fact]
    public void Every_rule_has_a_unique_id()
    {
        Assert.Equal(RuleCatalogue.All.Count, RuleCatalogue.All.Select(r => r.Id).Distinct().Count());
        Assert.NotEmpty(RuleCatalogue.All);
    }
}
