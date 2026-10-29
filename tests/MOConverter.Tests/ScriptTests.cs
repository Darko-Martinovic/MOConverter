using MOConverter.Assessment;
using MOConverter.Catalog;
using MOConverter.Rules;
using MOConverter.Scripting;

namespace MOConverter.Tests;

/// <summary>
/// Scripts every table of Fixtures/cases.sql that isn't blocked, runs the script with sqlcmd,
/// and compares what it made with what the source has.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class ScriptTests(SqlServer server)
{
    // A name that needs quoting in identifiers, string literals and the dynamic SQL inside them
    private const string Target = "Cases [O'Brien]";

    private static readonly SemaphoreSlim Once = new(1, 1);
    private static (DatabaseModel Source, DatabaseModel Target, Assessment.Assessment Assessment)? converted;

    private async Task<(DatabaseModel Source, DatabaseModel Target, Assessment.Assessment Assessment)> ConvertedAsync()
    {
        await Once.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            if (converted is { } done)
                return done;

            var source = await Assessor.ReadAsync(server.ConnectionString(SqlServer.Cases), TestContext.Current.CancellationToken);
            var assessment = Assessor.Assess(source, Selection.Parse(null, "cases"));
            var tables = assessment.Tables.Where(t => t.Outcome != Outcome.Blocked).Select(t => t.Name).ToList();
            var options = new ScriptOptions(Target,
                new HashSet<ObjectName> { new("cases", "Ready"), new("cases", "ManyIndexes") },
                new HashSet<ObjectName> { new("cases", "Tracked") });

            var result = await server.SqlcmdAsync(Scripter.Script(source, tables, options));
            Assert.True(result.ExitCode == 0, $"sqlcmd failed:\n{result.Stdout}\n{result.Stderr}");

            var target = await Assessor.ReadAsync(server.ConnectionString(Target), TestContext.Current.CancellationToken);
            converted = (source, target, assessment);
            return converted.Value;
        }
        finally
        {
            Once.Release();
        }
    }

    private static readonly string[] ConvertibleNames =
    [
        "Ready", "UpperLimit", "ManyIndexes", "Columnstore", "Order Details", "Parent", "AliasTypes", "OldLobTypes",
        "Indexes", "ComputedIndexed", "Sparse", "Partitioned", "Compressed", "Disabled", "LossyTypes", "AliasLossy",
        "Checks", "Defaults", "IdentitySeed", "BigSeed", "Child", "Audited", "Tracked",
    ];

    public static TheoryData<string> Convertible => [.. ConvertibleNames];

    // SQL Server 2025's json and vector table is lossy, so it is converted too
    private IEnumerable<string> ExpectedNames => server.MajorVersion >= 17 ? [.. ConvertibleNames, "NewTypes"] : ConvertibleNames;

    [Fact]
    public async Task Every_table_that_isnt_blocked_becomes_memory_optimized()
    {
        var (source, target, assessment) = await ConvertedAsync();

        var expected = assessment.Tables.Where(t => t.Outcome != Outcome.Blocked).Select(t => t.Name.Name).Order(StringComparer.Ordinal).ToList();
        Assert.Equal(expected, target.Tables.Where(t => t.IsMemoryOptimized).Select(t => t.Name.Name).Order(StringComparer.Ordinal));
        Assert.Equal(expected, ExpectedNames.Order(StringComparer.Ordinal));
        Assert.True(target.Server.HasMemoryOptimizedFilegroup);
        Assert.Equal(source.Server.DatabaseCollation, target.Server.DatabaseCollation);
    }

    [Fact]
    public async Task Json_and_vector_become_nvarchar()
    {
        Assert.SkipUnless(server.MajorVersion >= 17, "json and vector need SQL Server 2025");
        var (_, target, _) = await ConvertedAsync();

        var columns = target.Tables.Single(t => t.Name == new ObjectName("cases", "NewTypes")).Columns;

        Assert.Equal(["int", "nvarchar(max)", "nvarchar(max)"], columns.Select(c => TypeMap.Format(c.Type, c.MaxLength, c.Precision, c.Scale)));
    }

    [Theory]
    [MemberData(nameof(Convertible))]
    public async Task Columns_get_the_mapped_types(string name)
    {
        var (source, target, _) = await ConvertedAsync();
        var from = source.Tables.Single(t => t.Name == new ObjectName("cases", name));
        var to = target.Tables.Single(t => t.Name == from.Name);

        Assert.Equal(from.Columns.Select(c => c.Name), to.Columns.Select(c => c.Name));
        foreach (var (before, after) in from.Columns.Zip(to.Columns))
        {
            if (before.IsComputed)
            {
                Assert.True(after.IsComputed);
                continue;
            }

            Assert.Equal(TypeMap.Map(before).Target, TypeMap.Format(after.Type, after.MaxLength, after.Precision, after.Scale));
            Assert.Equal(before.IsIdentity, after.IsIdentity);
            // xml, json and vector have no collation; as nvarchar(max) they get the database's, which is the source's
            if (before.Collation is not null)
                Assert.Equal(before.Collation, after.Collation);
            if (before.Type != "timestamp")
                Assert.Equal(before.IsNullable, after.IsNullable);
        }
    }

    [Theory]
    [MemberData(nameof(Convertible))]
    public async Task Keys_indexes_and_checks_are_kept_unless_a_rule_drops_them(string name)
    {
        var (source, target, _) = await ConvertedAsync();
        var from = source.Tables.Single(t => t.Name == new ObjectName("cases", name));
        var to = target.Tables.Single(t => t.Name == from.Name);

        var fromKey = from.Indexes.Single(i => i.IsPrimaryKey);
        var toKey = to.Indexes.Single(i => i.IsPrimaryKey);
        Assert.Equal(fromKey.Name, toKey.Name);
        Assert.Equal(fromKey.Keys.Select(k => k.Name), toKey.Keys.Select(k => k.Name));

        var kept = from.Indexes.Where(i => !i.IsPrimaryKey && !i.IsDisabled && i.Kind is "CLUSTERED" or "NONCLUSTERED" or "CLUSTERED COLUMNSTORE");
        Assert.Equal(kept.Select(i => i.Name).Order(), to.Indexes.Where(i => !i.IsPrimaryKey).Select(i => i.Name).Order());
        Assert.All(to.Indexes.Where(i => i.Kind != "CLUSTERED COLUMNSTORE"), i => Assert.Null(i.Filter));

        var checks = from.Checks.Where(c => !c.IsDisabled && Expressions.UnsupportedInCondition(c.Definition).Count == 0);
        Assert.Equal(checks.Select(c => c.Name).Order(), to.Checks.Select(c => c.Name).Order());
        Assert.Empty(to.Triggers);
    }

    [Fact]
    public async Task Foreign_keys_become_no_action()
    {
        var (_, target, _) = await ConvertedAsync();

        var key = Assert.Single(target.ForeignKeys);
        Assert.Equal(("FK_Child_Parent", "NO_ACTION", "NO_ACTION"), (key.Name, key.DeleteAction, key.UpdateAction));
    }

    [Fact]
    public async Task Hash_primary_keys_get_twice_their_rows_in_buckets()
    {
        await ConvertedAsync();

        var buckets = await server.ScalarAsync(Target, """
            SELECT bucket_count FROM sys.hash_indexes WHERE object_id = OBJECT_ID(N'cases.Ready');
            """);

        Assert.Equal(2048L, Convert.ToInt64(buckets));
    }

    [Fact]
    public async Task Schema_only_tables_get_that_durability()
    {
        await ConvertedAsync();

        Assert.Equal("SCHEMA_ONLY", await server.ScalarAsync(Target, "SELECT durability_desc FROM sys.tables WHERE name = N'Tracked';"));
        Assert.Equal("SCHEMA_AND_DATA", await server.ScalarAsync(Target, "SELECT durability_desc FROM sys.tables WHERE name = N'Ready';"));
    }

    [Fact]
    public async Task The_converted_database_assesses_as_memory_optimized()
    {
        var (_, target, _) = await ConvertedAsync();

        var assessment = Assessor.Assess(target, Selection.All);

        Assert.Empty(assessment.Tables);
        Assert.Equal(ExpectedNames.Count(), assessment.AlreadyMemoryOptimized.Count);
    }

    [Fact]
    public async Task Indexed_computed_columns_become_persisted()
    {
        var (_, target, _) = await ConvertedAsync();

        var doubled = target.Tables.Single(t => t.Name == new ObjectName("cases", "ComputedIndexed")).Columns.Single(c => c.Name == "Doubled");

        Assert.True(doubled.IsPersisted);
    }
}
