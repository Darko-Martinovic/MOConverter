namespace MOConverter.Catalog;

/// <summary>
/// A schema-qualified name, exactly as the catalog spells it: a database with a case-sensitive
/// collation can have both dbo.Orders and dbo.orders.
/// </summary>
public sealed record ObjectName(string Schema, string Name)
{
    public override string ToString() => $"{Quote(Schema)}.{Quote(Name)}";

    public static string Quote(string name) => $"[{name.Replace("]", "]]")}]";
}

/// <summary>What the server and the database are, as far as In-Memory OLTP cares.</summary>
public sealed record ServerFacts(
    string ProductVersion,
    int MajorVersion,
    string Edition,
    int EngineEdition,
    bool XtpSupported,
    string? ServiceObjective,
    string Database,
    int CompatibilityLevel,
    bool HasMemoryOptimizedFilegroup,
    long? MemoryLimitBytes,
    string MemoryLimitSource,
    string DatabaseCollation = "")
{
    /// <summary>Azure SQL Database (EngineEdition 5); Managed Instance is 8.</summary>
    public bool IsAzureSqlDatabase => EngineEdition == 5;

    public bool IsAzure => EngineEdition is 5 or 8;
}

/// <summary>A column, with its type as the catalog describes it.</summary>
/// <param name="Type">The type's name: <c>int</c>, <c>geography</c>, <c>vector</c>, or an alias type's own name.</param>
/// <param name="BaseType">The system type underneath: the same as <paramref name="Type"/> for system types, null for CLR types.</param>
/// <param name="TypeSchema">The schema of an alias or CLR type (<c>sys</c> for the built-in ones).</param>
/// <param name="MaxLength">Bytes, as in sys.columns: -1 for max, twice the characters for nchar and nvarchar.</param>
/// <param name="IdentitySeed">As text, since a decimal(38,0) seed can be larger than .NET's decimal.</param>
public sealed record ColumnInfo(
    string Name,
    string Type,
    string? BaseType,
    string TypeSchema,
    bool IsAliasType,
    bool IsClrType,
    int MaxLength,
    int Precision,
    int Scale,
    bool IsNullable,
    bool IsIdentity = false,
    string? IdentitySeed = null,
    string? IdentityIncrement = null,
    bool IsComputed = false,
    string? ComputedDefinition = null,
    bool IsPersisted = false,
    bool IsSparse = false,
    bool IsColumnSet = false,
    string? DefaultName = null,
    string? DefaultDefinition = null,
    string? Collation = null);

public sealed record IndexColumn(string Name, bool IsDescending);

/// <param name="Kind">sys.indexes.type_desc: CLUSTERED, NONCLUSTERED, XML, SPATIAL, CLUSTERED COLUMNSTORE, NONCLUSTERED COLUMNSTORE.</param>
/// <param name="Filter">The filter's text: null when there is none, or when it isn't visible to this login.</param>
public sealed record IndexInfo(
    string Name,
    string Kind,
    bool IsPrimaryKey,
    bool IsUnique,
    bool IsUniqueConstraint,
    bool HasFilter,
    string? Filter,
    bool IsDisabled,
    IReadOnlyList<IndexColumn> Keys,
    IReadOnlyList<string> Included);

/// <param name="DeleteAction">sys.foreign_keys' action: NO_ACTION, CASCADE, SET_NULL or SET_DEFAULT.</param>
/// <param name="ReferencesPrimaryKey">False when the key references a unique constraint or index instead.</param>
/// <param name="IsNotTrusted">Made or re-enabled WITH NOCHECK, so rows may break it; true for a disabled one too, and always for NOT FOR REPLICATION.</param>
public sealed record ForeignKeyInfo(
    string Name,
    ObjectName Table,
    ObjectName Referenced,
    string DeleteAction,
    string UpdateAction,
    bool IsDisabled,
    bool ReferencesPrimaryKey,
    IReadOnlyList<(string Column, string Referenced)> Columns,
    bool IsNotTrusted = false,
    bool IsNotForReplication = false);

/// <param name="IsNotTrusted">Made or re-enabled WITH NOCHECK, so rows may break it; true for a disabled one too, and always for NOT FOR REPLICATION.</param>
public sealed record CheckInfo(string Name, string Definition, bool IsDisabled, bool IsNotTrusted = false, bool IsNotForReplication = false);

public sealed record TriggerInfo(string Name, bool IsInsteadOf, bool IsDisabled);

/// <param name="DataBytes">Pages the heap or clustered index uses, in bytes, LOB pages included.</param>
/// <param name="TemporalType">sys.tables.temporal_type: 1 a history table, 2 a system-versioned table.</param>
/// <param name="LedgerType">sys.tables.ledger_type (SQL Server 2022+): 0 when not a ledger table.</param>
/// <param name="PrimaryKeyHint">The table's MOIndexEp extended property, as version 1 read it: HASH asks for a hash primary key.</param>
public sealed record TableInfo(
    ObjectName Name,
    long Rows,
    long DataBytes,
    IReadOnlyList<ColumnInfo> Columns,
    IReadOnlyList<IndexInfo> Indexes,
    IReadOnlyList<CheckInfo> Checks,
    IReadOnlyList<TriggerInfo> Triggers,
    bool IsMemoryOptimized = false,
    bool IsPartitioned = false,
    string? Compression = null,
    int TemporalType = 0,
    int LedgerType = 0,
    bool IsGraphTable = false,
    bool IsFileTable = false,
    bool IsReplicated = false,
    bool IsChangeTracked = false,
    bool IsTrackedByCdc = false,
    bool HasFullTextIndex = false,
    string? PrimaryKeyHint = null);

public sealed record DatabaseModel(
    ServerFacts Server,
    IReadOnlyList<TableInfo> Tables,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys);
