namespace MOConverter.Catalog;

/// <summary>A schema-qualified name, compared without regard to case as SQL Server's default collations do.</summary>
public sealed record ObjectName(string Schema, string Name)
{
    public override string ToString() => $"{Quote(Schema)}.{Quote(Name)}";

    public bool Equals(ObjectName? other) =>
        other is not null
        && string.Equals(Schema, other.Schema, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Name, other.Name, StringComparison.OrdinalIgnoreCase);

    public override int GetHashCode() =>
        HashCode.Combine(Schema.ToUpperInvariant(), Name.ToUpperInvariant());

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
    string MemoryLimitSource)
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
    decimal? IdentitySeed = null,
    decimal? IdentityIncrement = null,
    bool IsComputed = false,
    string? ComputedDefinition = null,
    bool IsPersisted = false,
    bool IsSparse = false,
    bool IsColumnSet = false,
    string? DefaultName = null,
    string? DefaultDefinition = null);

public sealed record IndexColumn(string Name, bool IsDescending);

/// <param name="Kind">sys.indexes.type_desc: CLUSTERED, NONCLUSTERED, XML, SPATIAL, CLUSTERED COLUMNSTORE, NONCLUSTERED COLUMNSTORE.</param>
public sealed record IndexInfo(
    string Name,
    string Kind,
    bool IsPrimaryKey,
    bool IsUnique,
    bool IsUniqueConstraint,
    string? Filter,
    bool IsDisabled,
    IReadOnlyList<IndexColumn> Keys,
    IReadOnlyList<string> Included);

/// <param name="DeleteAction">sys.foreign_keys' action: NO_ACTION, CASCADE, SET_NULL or SET_DEFAULT.</param>
public sealed record ForeignKeyInfo(
    string Name,
    ObjectName Table,
    ObjectName Referenced,
    string DeleteAction,
    string UpdateAction,
    bool IsDisabled,
    IReadOnlyList<(string Column, string Referenced)> Columns);

public sealed record CheckInfo(string Name, string Definition, bool IsDisabled);

public sealed record TriggerInfo(string Name, bool IsInsteadOf, bool IsDisabled);

/// <param name="DataBytes">Pages the heap or clustered index uses, in bytes, LOB pages included.</param>
/// <param name="TemporalType">sys.tables.temporal_type: 1 a history table, 2 a system-versioned table.</param>
/// <param name="LedgerType">sys.tables.ledger_type (SQL Server 2022+): 0 when not a ledger table.</param>
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
    bool HasFullTextIndex = false);

public sealed record DatabaseModel(
    ServerFacts Server,
    IReadOnlyList<TableInfo> Tables,
    IReadOnlyList<ForeignKeyInfo> ForeignKeys);
