using Microsoft.Data.SqlClient;

namespace MOConverter.Catalog;

/// <summary>
/// Reads what the assessment needs from the catalog views, in a few queries for the whole
/// database. Reading only: it needs VIEW DEFINITION on the database, and VIEW SERVER STATE
/// for the memory check.
/// </summary>
public static class CatalogReader
{
    public static async Task<DatabaseModel> ReadAsync(SqlConnection connection, CancellationToken cancellationToken = default)
    {
        var (facts, hasLedger) = await ReadFactsAsync(connection, cancellationToken);
        var tables = new Dictionary<int, TableBuilder>();
        var foreignKeys = new List<ForeignKeyInfo>();

        await using var command = new SqlCommand(ModelQuery(hasLedger), connection) { CommandTimeout = 300 };
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            tables[reader.GetInt32(0)] = new TableBuilder(new ObjectName(reader.GetString(1), reader.GetString(2)))
            {
                IsMemoryOptimized = reader.GetBoolean(3),
                TemporalType = reader.GetByte(4),
                LedgerType = reader.GetByte(5),
                IsGraphTable = reader.GetInt32(6) == 1,
                IsFileTable = reader.GetBoolean(7),
                IsReplicated = reader.GetBoolean(8),
                IsChangeTracked = reader.GetInt32(9) == 1,
                IsTrackedByCdc = reader.GetBoolean(10),
                HasFullTextIndex = reader.GetInt32(11) == 1,
                Rows = reader.GetInt64(12),
                DataBytes = reader.GetInt64(13),
                IsPartitioned = reader.GetInt32(14) > 1,
                Compression = NullableString(reader, 15),
                PrimaryKeyHint = NullableString(reader, 16),
            };
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables[reader.GetInt32(0)].Columns.Add(new ColumnInfo(
                Name: reader.GetString(1),
                Type: reader.GetString(2),
                BaseType: NullableString(reader, 3),
                TypeSchema: reader.GetString(4),
                IsAliasType: reader.GetBoolean(5) && !reader.GetBoolean(6),
                IsClrType: reader.GetBoolean(6),
                MaxLength: reader.GetInt16(7),
                Precision: reader.GetByte(8),
                Scale: reader.GetByte(9),
                IsNullable: reader.GetBoolean(10),
                IsIdentity: reader.GetBoolean(11),
                IdentitySeed: NullableString(reader, 12),
                IdentityIncrement: NullableString(reader, 13),
                IsComputed: reader.GetBoolean(14),
                ComputedDefinition: NullableString(reader, 15),
                IsPersisted: reader.GetBoolean(16),
                IsSparse: reader.GetBoolean(17),
                IsColumnSet: reader.GetBoolean(18),
                DefaultName: NullableString(reader, 19),
                DefaultDefinition: NullableString(reader, 20),
                Collation: NullableString(reader, 21)));
        }

        var indexes = new Dictionary<(int, int), IndexBuilder>();
        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var index = new IndexBuilder(
                reader.GetString(2), reader.GetString(3), reader.GetBoolean(4), reader.GetBoolean(5),
                reader.GetBoolean(6), reader.GetBoolean(7), NullableString(reader, 8), reader.GetBoolean(9));
            indexes[(reader.GetInt32(0), reader.GetInt32(1))] = index;
            tables[reader.GetInt32(0)].Indexes.Add(index);
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var index = indexes[(reader.GetInt32(0), reader.GetInt32(1))];
            // A columnstore index lists its columns as included: they are what it stores
            if (reader.GetBoolean(4) && !index.Kind.EndsWith("COLUMNSTORE", StringComparison.Ordinal))
                index.Included.Add(reader.GetString(2));
            else
                index.Keys.Add(new IndexColumn(reader.GetString(2), reader.GetBoolean(3)));
        }

        var keys = new Dictionary<int, (string Name, int Table, int Referenced, string Delete, string Update, bool Disabled, bool ToPrimaryKey, bool NotTrusted, List<(string, string)> Columns)>();
        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            keys[reader.GetInt32(0)] = (reader.GetString(1), reader.GetInt32(2), reader.GetInt32(3),
                reader.GetString(4), reader.GetString(5), reader.GetBoolean(6), reader.GetInt32(7) == 1, reader.GetBoolean(8), []);
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            keys[reader.GetInt32(0)].Columns.Add((reader.GetString(1), reader.GetString(2)));

        foreach (var key in keys.Values)
        {
            foreignKeys.Add(new ForeignKeyInfo(key.Name, tables[key.Table].Name, tables[key.Referenced].Name,
                key.Delete, key.Update, key.Disabled, key.ToPrimaryKey, key.Columns, key.NotTrusted));
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            // A null definition means it isn't visible to this login
            tables[reader.GetInt32(0)].Checks.Add(new CheckInfo(reader.GetString(1), NullableString(reader, 2) ?? "", reader.GetBoolean(3), reader.GetBoolean(4)));
        }

        await reader.NextResultAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
            tables[reader.GetInt32(0)].Triggers.Add(new TriggerInfo(reader.GetString(1), reader.GetBoolean(2), reader.GetBoolean(3)));

        return new DatabaseModel(facts, tables.Values.Select(t => t.Build()).OrderBy(t => t.Name.Schema).ThenBy(t => t.Name.Name).ToList(), foreignKeys);
    }

    private static async Task<(ServerFacts Facts, bool HasLedger)> ReadFactsAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string query = """
            SELECT CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(128)),
                   CAST(SERVERPROPERTY('ProductMajorVersion') AS int),
                   CAST(SERVERPROPERTY('Edition') AS nvarchar(128)),
                   CAST(SERVERPROPERTY('EngineEdition') AS int),
                   CAST(COALESCE(DATABASEPROPERTYEX(DB_NAME(), 'IsXTPSupported'), SERVERPROPERTY('IsXTPSupported')) AS int),
                   CAST(DATABASEPROPERTYEX(DB_NAME(), 'ServiceObjective') AS nvarchar(128)),
                   DB_NAME(),
                   (SELECT CAST(compatibility_level AS int) FROM sys.databases WHERE database_id = DB_ID()),
                   CASE WHEN EXISTS (SELECT 1 FROM sys.filegroups WHERE type = 'FX') THEN 1 ELSE 0 END,
                   CASE WHEN COL_LENGTH('sys.tables', 'ledger_type') IS NULL THEN 0 ELSE 1 END,
                   CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128));
            """;

        ServerFacts facts;
        bool hasLedger;
        await using (var command = new SqlCommand(query, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            await reader.ReadAsync(cancellationToken);
            facts = new ServerFacts(
                ProductVersion: reader.GetString(0),
                MajorVersion: reader.IsDBNull(1) ? 0 : reader.GetInt32(1),
                Edition: reader.GetString(2),
                EngineEdition: reader.GetInt32(3),
                XtpSupported: !reader.IsDBNull(4) && reader.GetInt32(4) == 1,
                ServiceObjective: NullableString(reader, 5),
                Database: reader.GetString(6),
                CompatibilityLevel: reader.GetInt32(7),
                HasMemoryOptimizedFilegroup: reader.GetInt32(8) == 1,
                MemoryLimitBytes: null,
                MemoryLimitSource: "",
                DatabaseCollation: NullableString(reader, 10) ?? "");
            hasLedger = reader.GetInt32(9) == 1;
        }

        if (facts.IsAzureSqlDatabase)
        {
            return (facts with { MemoryLimitSource = "not checked: on Azure SQL Database, the service objective sets the In-Memory OLTP storage limit" }, hasLedger);
        }

        try
        {
            await using var command = new SqlCommand("SELECT committed_target_kb FROM sys.dm_os_sys_info;", connection);
            var kilobytes = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
            return (facts with { MemoryLimitBytes = kilobytes * 1024, MemoryLimitSource = "the memory SQL Server aims to commit (sys.dm_os_sys_info)" }, hasLedger);
        }
        catch (SqlException e) when (e.Number is 297 or 300)
        {
            return (facts with { MemoryLimitSource = "not checked: reading sys.dm_os_sys_info needs VIEW SERVER STATE" }, hasLedger);
        }
    }

    private static string ModelQuery(bool hasLedger) => $"""
        SELECT t.object_id, s.name, t.name, t.is_memory_optimized, t.temporal_type,
               {(hasLedger ? "t.ledger_type" : "CAST(0 AS tinyint)")},
               CASE WHEN t.is_node = 1 OR t.is_edge = 1 THEN 1 ELSE 0 END,
               t.is_filetable, t.is_replicated,
               CASE WHEN ct.object_id IS NULL THEN 0 ELSE 1 END,
               t.is_tracked_by_cdc,
               CASE WHEN EXISTS (SELECT 1 FROM sys.fulltext_indexes fi WHERE fi.object_id = t.object_id) THEN 1 ELSE 0 END,
               ISNULL(p.rows, 0), ISNULL(p.pages, 0) * 8192, ISNULL(p.partitions, 0), p.compression,
               (SELECT CONVERT(nvarchar(100), ep.value) FROM sys.extended_properties ep
                WHERE ep.class = 1 AND ep.major_id = t.object_id AND ep.minor_id = 0 AND ep.name = N'MOIndexEp')
        FROM sys.tables t
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        LEFT JOIN sys.change_tracking_tables ct ON ct.object_id = t.object_id
        OUTER APPLY (
            SELECT SUM(pa.rows) AS rows,
                   COUNT(*) AS partitions,
                   MAX(CASE WHEN pa.data_compression_desc IN ('NONE', 'COLUMNSTORE') THEN NULL ELSE pa.data_compression_desc END) AS compression,
                   (SELECT SUM(CAST(au.used_pages AS bigint))
                    FROM sys.partitions p2
                    -- In-row and row-overflow units belong to the hobt, LOB units to the partition
                    JOIN sys.allocation_units au ON au.container_id = CASE WHEN au.type IN (1, 3) THEN p2.hobt_id ELSE p2.partition_id END
                    WHERE p2.object_id = t.object_id AND p2.index_id IN (0, 1)) AS pages
            FROM sys.partitions pa
            WHERE pa.object_id = t.object_id AND pa.index_id IN (0, 1)) p
        WHERE t.is_ms_shipped = 0;

        SELECT c.object_id, c.name, ty.name, bt.name, SCHEMA_NAME(ty.schema_id), ty.is_user_defined, ty.is_assembly_type,
               c.max_length, c.precision, c.scale, c.is_nullable, c.is_identity,
               CONVERT(nvarchar(40), ic.seed_value), CONVERT(nvarchar(40), ic.increment_value),
               c.is_computed, cc.definition, ISNULL(cc.is_persisted, 0), c.is_sparse, c.is_column_set,
               dc.name, dc.definition, c.collation_name
        FROM sys.columns c
        JOIN sys.tables t ON t.object_id = c.object_id AND t.is_ms_shipped = 0
        JOIN sys.types ty ON ty.user_type_id = c.user_type_id
        LEFT JOIN sys.types bt ON bt.user_type_id = c.system_type_id
        LEFT JOIN sys.identity_columns ic ON ic.object_id = c.object_id AND ic.column_id = c.column_id
        LEFT JOIN sys.computed_columns cc ON cc.object_id = c.object_id AND cc.column_id = c.column_id
        LEFT JOIN sys.default_constraints dc ON dc.parent_object_id = c.object_id AND dc.parent_column_id = c.column_id
        ORDER BY c.object_id, c.column_id;

        SELECT i.object_id, i.index_id, i.name, i.type_desc, i.is_primary_key, i.is_unique, i.is_unique_constraint,
               i.has_filter, i.filter_definition, i.is_disabled
        FROM sys.indexes i
        JOIN sys.tables t ON t.object_id = i.object_id AND t.is_ms_shipped = 0
        WHERE i.index_id > 0 AND i.is_hypothetical = 0
        ORDER BY i.object_id, i.index_id;

        SELECT ic.object_id, ic.index_id, c.name, ic.is_descending_key, ic.is_included_column
        FROM sys.index_columns ic
        JOIN sys.indexes i ON i.object_id = ic.object_id AND i.index_id = ic.index_id AND i.is_hypothetical = 0
        JOIN sys.tables t ON t.object_id = ic.object_id AND t.is_ms_shipped = 0
        JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
        -- Leave out the partitioning column SQL Server adds to aligned indexes
        WHERE ic.key_ordinal > 0 OR ic.is_included_column = 1 OR i.type IN (5, 6)
        ORDER BY ic.object_id, ic.index_id, ic.key_ordinal, ic.index_column_id;

        SELECT fk.object_id, fk.name, fk.parent_object_id, fk.referenced_object_id,
               fk.delete_referential_action_desc, fk.update_referential_action_desc, fk.is_disabled,
               CASE WHEN EXISTS (SELECT 1 FROM sys.indexes ri
                                 WHERE ri.object_id = fk.referenced_object_id AND ri.index_id = fk.key_index_id AND ri.is_primary_key = 1)
                    THEN 1 ELSE 0 END,
               -- NOT FOR REPLICATION keeps a constraint untrusted for good, rows or not: not a sign of bad rows
               CAST(CASE WHEN fk.is_not_trusted = 1 AND fk.is_not_for_replication = 0 THEN 1 ELSE 0 END AS bit)
        FROM sys.foreign_keys fk
        JOIN sys.tables t ON t.object_id = fk.parent_object_id AND t.is_ms_shipped = 0;

        SELECT fkc.constraint_object_id, pc.name, rc.name
        FROM sys.foreign_key_columns fkc
        JOIN sys.tables t ON t.object_id = fkc.parent_object_id AND t.is_ms_shipped = 0
        JOIN sys.columns pc ON pc.object_id = fkc.parent_object_id AND pc.column_id = fkc.parent_column_id
        JOIN sys.columns rc ON rc.object_id = fkc.referenced_object_id AND rc.column_id = fkc.referenced_column_id
        ORDER BY fkc.constraint_object_id, fkc.constraint_column_id;

        SELECT cc.parent_object_id, cc.name, cc.definition, cc.is_disabled,
               CAST(CASE WHEN cc.is_not_trusted = 1 AND cc.is_not_for_replication = 0 THEN 1 ELSE 0 END AS bit)
        FROM sys.check_constraints cc
        JOIN sys.tables t ON t.object_id = cc.parent_object_id AND t.is_ms_shipped = 0
        ORDER BY cc.parent_object_id, cc.name;

        SELECT tr.parent_id, tr.name, tr.is_instead_of_trigger, tr.is_disabled
        FROM sys.triggers tr
        JOIN sys.tables t ON t.object_id = tr.parent_id AND t.is_ms_shipped = 0
        WHERE tr.parent_class = 1
        ORDER BY tr.parent_id, tr.name;
        """;

    private static string? NullableString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private sealed class TableBuilder(ObjectName name)
    {
        public ObjectName Name { get; } = name;
        public List<ColumnInfo> Columns { get; } = [];
        public List<IndexBuilder> Indexes { get; } = [];
        public List<CheckInfo> Checks { get; } = [];
        public List<TriggerInfo> Triggers { get; } = [];
        public long Rows { get; init; }
        public long DataBytes { get; init; }
        public bool IsMemoryOptimized { get; init; }
        public bool IsPartitioned { get; init; }
        public string? Compression { get; init; }
        public int TemporalType { get; init; }
        public int LedgerType { get; init; }
        public bool IsGraphTable { get; init; }
        public bool IsFileTable { get; init; }
        public bool IsReplicated { get; init; }
        public bool IsChangeTracked { get; init; }
        public bool IsTrackedByCdc { get; init; }
        public bool HasFullTextIndex { get; init; }
        public string? PrimaryKeyHint { get; init; }

        public TableInfo Build() => new(
            Name, Rows, DataBytes, Columns, Indexes.Select(i => i.Build()).ToList(), Checks, Triggers,
            IsMemoryOptimized, IsPartitioned, Compression, TemporalType, LedgerType, IsGraphTable,
            IsFileTable, IsReplicated, IsChangeTracked, IsTrackedByCdc, HasFullTextIndex, PrimaryKeyHint);
    }

    private sealed class IndexBuilder(string name, string kind, bool isPrimaryKey, bool isUnique, bool isUniqueConstraint, bool hasFilter, string? filter, bool isDisabled)
    {
        public string Kind { get; } = kind;
        public List<IndexColumn> Keys { get; } = [];
        public List<string> Included { get; } = [];

        public IndexInfo Build() => new(name, Kind, isPrimaryKey, isUnique, isUniqueConstraint, hasFilter, filter, isDisabled, Keys, Included);
    }
}
