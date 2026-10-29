namespace MOConverter.Rules;

/// <summary>What converting a table, or the selection, would do. The order matters: the worst wins.</summary>
public enum Outcome
{
    /// <summary>Converts as it is.</summary>
    Ready,

    /// <summary>The tool changes something, and the data and query results stay the same.</summary>
    Fixed,

    /// <summary>Converts with a change in meaning, which <c>--accept-lossy</c> has to allow.</summary>
    Lossy,

    /// <summary>Can't be converted until something changes in the database or the selection.</summary>
    Blocked,
}

/// <summary>One limit of memory-optimized tables, and what the tool does about it.</summary>
public sealed record Rule(string Id, Outcome Outcome, string Summary);

/// <summary>A rule that applies, with what it found.</summary>
public sealed record Finding(Rule Rule, string Message)
{
    public Outcome Outcome => Rule.Outcome;
}

/// <summary>
/// Every rule. The limits were checked on SQL Server 2022 and 2025, and the tests check them
/// again on every run (ServerAgreesTests).
/// </summary>
public static class RuleCatalogue
{
    // The server and the selection
    public static readonly Rule ServerVersion = new("server-version", Outcome.Blocked, "SQL Server 2017 or later is needed.");
    public static readonly Rule ServerXtp = new("server-xtp", Outcome.Blocked, "The server or service tier doesn't support In-Memory OLTP.");
    public static readonly Rule SelectionUnmatched = new("selection-unmatched", Outcome.Blocked, "A table or schema named in the selection doesn't exist.");
    public static readonly Rule MemoryTotal = new("memory-total", Outcome.Blocked, "The selected tables need more memory than the server has.");
    public static readonly Rule MemoryNote = new("memory-note", Outcome.Ready, "How the memory check was done, or why it wasn't.");
    public static readonly Rule EditionQuota = new("edition-quota", Outcome.Ready, "Express, Web and Standard editions cap memory-optimized data per database.");
    public static readonly Rule AzureSqlDatabase = new("azure-sql-database", Outcome.Ready, "Azure SQL Database: only Premium and Business Critical tiers support In-Memory OLTP.");

    // Blocked tables
    public static readonly Rule NoPrimaryKey = new("no-primary-key", Outcome.Blocked, "A memory-optimized table needs a primary key.");
    public static readonly Rule Memory = new("memory", Outcome.Blocked, "The table alone needs more memory than the server has.");
    public static readonly Rule Temporal = new("temporal", Outcome.Blocked, "System-versioned (temporal) tables and their history tables are converted by hand.");
    public static readonly Rule Ledger = new("ledger", Outcome.Blocked, "Ledger tables can't be memory-optimized.");
    public static readonly Rule Graph = new("graph", Outcome.Blocked, "Graph (node and edge) tables can't be memory-optimized.");
    public static readonly Rule FileTable = new("filetable", Outcome.Blocked, "FileTables can't be memory-optimized.");
    public static readonly Rule Replicated = new("replicated", Outcome.Blocked, "Tables published for replication are converted by hand.");
    public static readonly Rule ColumnSet = new("column-set", Outcome.Blocked, "Column sets (and the sparse columns behind them) aren't supported.");
    public static readonly Rule TypeUnsupported = new("type-unsupported", Outcome.Blocked, "A column's type has no memory-optimized equivalent: sql_variant, or a CLR type of your own.");
    public static readonly Rule ComputedUnsupported = new("computed-unsupported", Outcome.Blocked, "A computed column uses something memory-optimized tables don't accept.");
    public static readonly Rule UniqueFilteredIndex = new("unique-filtered-index", Outcome.Blocked, "A unique filtered index: without its filter, rows it allowed could clash.");
    public static readonly Rule ForeignKeyBoundary = new("foreign-key-boundary", Outcome.Blocked, "A foreign key links the table to one that won't be memory-optimized.");
    public static readonly Rule ForeignKeyTarget = new("foreign-key-target", Outcome.Blocked, "A foreign key references a unique key: between memory-optimized tables, it must reference the primary key.");
    public static readonly Rule DefinitionHidden = new("definition-hidden", Outcome.Blocked, "A definition isn't visible to this login: it needs VIEW DEFINITION.");

    // Lossy changes
    public static readonly Rule TypeLossy = new("type-lossy", Outcome.Lossy, "A column's type is stored as another type, and loses its methods or part of its value.");
    public static readonly Rule CheckUnsupported = new("check-unsupported", Outcome.Lossy, "A check constraint uses something memory-optimized tables don't accept, and is left out.");
    public static readonly Rule DefaultUnsupported = new("default-unsupported", Outcome.Lossy, "A default uses something memory-optimized tables don't accept, and is left out.");
    public static readonly Rule IdentitySeed = new("identity-seed", Outcome.Lossy, "IDENTITY must be (1,1): existing values are kept, and new ones count up by 1.");
    public static readonly Rule ForeignKeyAction = new("foreign-key-action", Outcome.Lossy, "CASCADE, SET NULL and SET DEFAULT aren't supported: the key becomes NO ACTION.");
    public static readonly Rule Trigger = new("trigger", Outcome.Lossy, "Triggers aren't carried over: rewrite them as natively compiled AFTER triggers.");
    public static readonly Rule ChangeTracking = new("change-tracking", Outcome.Lossy, "Change tracking isn't supported on memory-optimized tables.");
    public static readonly Rule ChangeDataCapture = new("change-data-capture", Outcome.Lossy, "Change data capture isn't supported on memory-optimized tables.");
    public static readonly Rule FullText = new("full-text", Outcome.Lossy, "Full-text indexes aren't supported on memory-optimized tables.");
    public static readonly Rule ComputedOverLossy = new("computed-over-lossy", Outcome.Lossy, "A computed column uses a column whose type changes, so its values can change.");
    public static readonly Rule WideUniqueKey = new("wide-unique-key", Outcome.Lossy, "A unique index wider than a range index's 2500 bytes is left out, and with it the uniqueness it enforced.");

    // Fixed changes
    public static readonly Rule AliasType = new("alias-type", Outcome.Fixed, "Alias types aren't supported: the column gets the alias's base type.");
    public static readonly Rule OldLobType = new("old-lob-type", Outcome.Fixed, "text, ntext and image become varchar(max), nvarchar(max) and varbinary(max).");
    public static readonly Rule Sparse = new("sparse", Outcome.Fixed, "SPARSE isn't supported: the column is stored in full.");
    public static readonly Rule Utf8Collation = new("utf8-collation", Outcome.Fixed, "char and varchar can't have a UTF-8 collation: they become nchar and nvarchar, with the same collation.");
    public static readonly Rule WideKey = new("wide-key", Outcome.Fixed, "A range index's key can't pass 2500 bytes: a primary key becomes a hash index, another index is left out.");
    public static readonly Rule ColumnstoreLimits = new("columnstore-limits", Outcome.Fixed, "A clustered columnstore index is left out when a column is max, a column is computed, or a row can pass 8060 bytes.");
    public static readonly Rule ComputedIndexPersisted = new("computed-index-persisted", Outcome.Fixed, "An index on a computed column needs it PERSISTED.");
    public static readonly Rule ClusteredIndex = new("clustered-index", Outcome.Fixed, "Memory-optimized tables have no clustered rowstore index: it becomes nonclustered.");
    public static readonly Rule IncludedColumns = new("included-columns", Outcome.Fixed, "Every index on a memory-optimized table covers all columns: INCLUDE is dropped.");
    public static readonly Rule FilteredIndex = new("filtered-index", Outcome.Fixed, "Filtered indexes aren't supported: the index covers every row.");
    public static readonly Rule NonclusteredColumnstore = new("nonclustered-columnstore", Outcome.Fixed, "Only a clustered columnstore index is supported: the nonclustered one is left out.");
    public static readonly Rule XmlOrSpatialIndex = new("xml-spatial-index", Outcome.Fixed, "XML and spatial indexes are left out, with the types they index.");
    public static readonly Rule DisabledObject = new("disabled", Outcome.Fixed, "Disabled indexes and constraints aren't carried over.");
    public static readonly Rule Partitioned = new("partitioned", Outcome.Fixed, "Memory-optimized tables aren't partitioned: the table becomes one partition.");
    public static readonly Rule Compression = new("compression", Outcome.Fixed, "Row and page compression aren't supported: the data is stored uncompressed.");

    public static IReadOnlyList<Rule> All { get; } =
        typeof(RuleCatalogue).GetFields()
            .Where(f => f.FieldType == typeof(Rule))
            .Select(f => (Rule)f.GetValue(null)!)
            .ToList();
}
