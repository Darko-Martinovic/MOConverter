using System.Text;

using MOConverter.Catalog;
using MOConverter.Rules;

namespace MOConverter.Scripting;

/// <param name="TargetDatabase">The database the script creates the tables in, made if it doesn't exist.</param>
/// <param name="HashPrimaryKeys">Tables whose primary key becomes a hash index; the others get a range (nonclustered) index.</param>
/// <param name="SchemaOnly">Tables made DURABILITY = SCHEMA_ONLY: their rows don't survive a restart.</param>
public sealed record ScriptOptions(
    string TargetDatabase,
    IReadOnlySet<ObjectName> HashPrimaryKeys,
    IReadOnlySet<ObjectName> SchemaOnly);

/// <summary>
/// Writes the T-SQL that creates memory-optimized copies of tables, from what the catalog says
/// of them. It makes the changes the rules describe, and says so in a comment where it makes
/// each: FIXED for a change that keeps the data and results, LOSSY for one that doesn't.
/// The tables it is given must not be blocked; the caller checks that with the assessment.
/// </summary>
public static class Scripter
{
    /// <summary>The fewest buckets a hash index gets: SQL Server's own minimum.</summary>
    public const int MinimumBuckets = 64;

    /// <summary>The most buckets SQL Server allows.</summary>
    public const int MaximumBuckets = 1 << 30;

    public static string Script(DatabaseModel model, IReadOnlyCollection<ObjectName> tables, ScriptOptions options)
    {
        var (setup, foreignKeys) = Parts(model, tables, options);
        return foreignKeys.Length == 0 ? setup : $"{setup}{Environment.NewLine}{foreignKeys}";
    }

    /// <summary>
    /// The script in two parts: everything up to the empty tables, which ends in the target
    /// database (it has a USE); and the foreign keys, which convert adds after the data.
    /// </summary>
    public static (string Setup, string ForeignKeys) Parts(DatabaseModel model, IReadOnlyCollection<ObjectName> tables, ScriptOptions options) =>
        (Setup(model, tables, options), ForeignKeys(model, tables));

    private static string Setup(DatabaseModel model, IReadOnlyCollection<ObjectName> tables, ScriptOptions options)
    {
        var chosen = model.Tables.Where(t => tables.Contains(t.Name)).ToList();
        var target = Quote(options.TargetDatabase);
        var filegroup = options.TargetDatabase + "_mo";
        var text = new StringBuilder();

        text.AppendLine($"-- Memory-optimized copies of {chosen.Count} tables of {Quote(model.Server.Database)}, written by MOConverter.");
        text.AppendLine("-- It creates the database if needed, its memory-optimized filegroup, the schemas, tables and foreign keys.");
        text.AppendLine("-- The tables are empty: moconverter convert copies the data, which T-SQL can't do across databases");
        text.AppendLine("-- once one side is memory-optimized.");
        text.AppendLine("-- FIXED marks a change that keeps the data and query results; LOSSY one that doesn't.");
        text.AppendLine("-- Run it with sqlcmd -b -x: -x keeps sqlcmd from reading $( in a name as a variable.");
        text.AppendLine("SET ANSI_NULLS ON;");
        text.AppendLine("SET QUOTED_IDENTIFIER ON;");
        text.AppendLine("GO");
        text.AppendLine($"IF DB_ID({Literal(options.TargetDatabase)}) IS NULL");
        text.AppendLine(model.Server.DatabaseCollation.Length > 0
            ? $"    CREATE DATABASE {target} COLLATE {model.Server.DatabaseCollation};"
            : $"    CREATE DATABASE {target};");
        text.AppendLine("GO");
        text.AppendLine("-- A memory-optimized filegroup refuses AUTO_CLOSE, which Express turns on for new databases");
        text.AppendLine($"ALTER DATABASE {target} SET AUTO_CLOSE OFF;");
        text.AppendLine($"ALTER DATABASE {target} SET MEMORY_OPTIMIZED_ELEVATE_TO_SNAPSHOT = ON;");
        text.AppendLine("GO");
        text.AppendLine($"USE {target};");
        text.AppendLine("GO");
        text.AppendLine($"IF NOT EXISTS (SELECT 1 FROM sys.filegroups WHERE type = 'FX')");
        text.AppendLine($"    ALTER DATABASE {target} ADD FILEGROUP {Quote(filegroup)} CONTAINS MEMORY_OPTIMIZED_DATA;");
        text.AppendLine("GO");
        text.AppendLine("-- The container goes in the server's default data folder. It is checked apart from the filegroup,");
        text.AppendLine("-- so a run that stopped between the two adds it.");
        text.AppendLine("IF NOT EXISTS (SELECT 1 FROM sys.database_files f JOIN sys.filegroups g ON g.data_space_id = f.data_space_id WHERE g.type = 'FX')");
        text.AppendLine("BEGIN");
        text.AppendLine($"    DECLARE @filegroup sysname = (SELECT TOP (1) name FROM sys.filegroups WHERE type = 'FX');");
        text.AppendLine($"    DECLARE @container nvarchar(4000) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) + {Literal(filegroup)};");
        text.AppendLine($"    DECLARE @add nvarchar(max) = {Literal($"ALTER DATABASE {target} ADD FILE (NAME = {Literal(filegroup)}, FILENAME = N'")}");
        text.AppendLine($"        + REPLACE(@container, N'''', N'''''') + N''') TO FILEGROUP ' + QUOTENAME(@filegroup) + N';';");
        text.AppendLine("    EXEC (@add);");
        text.AppendLine("END");
        text.AppendLine("GO");

        foreach (var schema in chosen.Select(t => t.Name.Schema).Distinct(StringComparer.Ordinal).Where(s => s != "dbo").Order(StringComparer.Ordinal))
            text.AppendLine($"IF SCHEMA_ID({Literal(schema)}) IS NULL EXEC (N'CREATE SCHEMA {Quote(schema).Replace("'", "''")}');");
        if (chosen.Any(t => t.Name.Schema != "dbo"))
            text.AppendLine("GO");

        foreach (var table in chosen)
        {
            text.AppendLine();
            Table(text, table, model.ForeignKeys, options, model.Server.DatabaseCollation);
            text.AppendLine("GO");
        }

        return text.ToString();
    }

    private static string ForeignKeys(DatabaseModel model, IReadOnlyCollection<ObjectName> tables)
    {
        var keys = model.ForeignKeys
            .Where(k => !k.IsDisabled && tables.Contains(k.Table) && tables.Contains(k.Referenced))
            .OrderBy(k => k.Table.ToString(), StringComparer.Ordinal).ThenBy(k => k.Name, StringComparer.Ordinal)
            .ToList();
        if (keys.Count == 0)
            return "";

        var text = new StringBuilder();
        text.AppendLine("-- Foreign keys, once every table exists");
        foreach (var key in keys)
            ForeignKey(text, key);
        text.AppendLine("GO");
        return text.ToString();
    }

    /// <summary>
    /// Twice the rows, rounded up to a power of two as SQL Server would: Microsoft's advice is
    /// one to two buckets per distinct key, and a primary key's keys are all distinct.
    /// </summary>
    public static int BucketCount(long rows)
    {
        var wanted = Math.Clamp(rows * 2, MinimumBuckets, MaximumBuckets);
        return (int)Math.Min(MaximumBuckets, (long)System.Numerics.BitOperations.RoundUpToPowerOf2((ulong)wanted));
    }

    private static void Table(StringBuilder text, TableInfo table, IReadOnlyList<ForeignKeyInfo> foreignKeys, ScriptOptions options, string databaseCollation)
    {
        var header = new List<string>();
        var items = new List<(List<string> Notes, string Definition)>();

        var schemaOnly = options.SchemaOnly.Contains(table.Name);
        var primaryKey = table.Indexes.Single(i => i.IsPrimaryKey);
        var indexedColumns = table.Indexes.Where(i => !i.IsDisabled).SelectMany(i => i.Keys).Select(k => k.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var column in table.Columns)
        {
            items.Add(Column(table, column, indexedColumns.Contains(column.Name),
                primaryKey.Keys.Any(k => k.Name == column.Name), databaseCollation));
        }

        var keyBytes = Limits.KeyBytes(table, primaryKey);
        var hash = options.HashPrimaryKeys.Contains(table.Name)
            || string.Equals(table.PrimaryKeyHint, "HASH", StringComparison.OrdinalIgnoreCase)
            || keyBytes > Limits.MaxKeyBytes;
        var keyNotes = new List<string>();
        if (keyBytes > Limits.MaxKeyBytes && !options.HashPrimaryKeys.Contains(table.Name))
            keyNotes.Add($"FIXED: the key takes {Bytes(keyBytes)}, more than a range index's {Limits.MaxKeyBytes}, so it is a hash index");
        var keyColumns = string.Join(", ", primaryKey.Keys.Select(k => hash ? Quote(k.Name) : Key(k)));
        items.Add((keyNotes, hash
            ? $"CONSTRAINT {Quote(primaryKey.Name)} PRIMARY KEY NONCLUSTERED HASH ({keyColumns}) WITH (BUCKET_COUNT = {BucketCount(table.Rows)})"
            : $"CONSTRAINT {Quote(primaryKey.Name)} PRIMARY KEY NONCLUSTERED ({keyColumns})"));

        foreach (var index in table.Indexes.Where(i => !i.IsPrimaryKey))
            Index(table, index, schemaOnly, header, items);

        foreach (var check in table.Checks)
        {
            var name = Quote(check.Name);
            if (check.IsDisabled)
            {
                header.Add($"FIXED: check constraint {name} is disabled, and isn't carried over");
                continue;
            }

            var unsupported = Expressions.UnsupportedInCondition(check.Definition);
            if (unsupported.Count > 0)
                header.Add($"LOSSY: check constraint {name} {check.Definition} uses {Expressions.Describe(unsupported)}, and is left out");
            else
                items.Add(([], $"CONSTRAINT {name} CHECK {check.Definition}"));
        }

        foreach (var trigger in table.Triggers)
            header.Add($"LOSSY: trigger {Quote(trigger.Name)} isn't carried over: rewrite it as a natively compiled AFTER trigger");
        if (table.IsChangeTracked)
            header.Add("LOSSY: change tracking isn't carried over");
        if (table.IsTrackedByCdc)
            header.Add("LOSSY: change data capture isn't carried over");
        if (table.HasFullTextIndex)
            header.Add("LOSSY: the full-text index isn't carried over");
        if (table.IsPartitioned)
            header.Add("FIXED: the table is partitioned, and becomes one partition");
        if (table.Compression is not null)
            header.Add($"FIXED: {table.Compression.ToLowerInvariant()} compression is dropped");
        foreach (var key in foreignKeys.Where(k => k.Table == table.Name && k.IsDisabled))
            header.Add($"FIXED: foreign key {Quote(key.Name)} is disabled, and isn't carried over");

        var durability = schemaOnly ? "SCHEMA_ONLY" : "SCHEMA_AND_DATA";
        var rows = table.Rows.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
        text.AppendLine($"-- {table.Name}: {rows} rows{(durability == "SCHEMA_ONLY" ? ", SCHEMA_ONLY: its rows don't survive a restart" : "")}");
        foreach (var note in header)
            text.AppendLine($"-- {OneLine(note)}");
        text.AppendLine($"CREATE TABLE {table.Name}");
        text.AppendLine("(");
        for (var i = 0; i < items.Count; i++)
        {
            foreach (var note in items[i].Notes)
                text.AppendLine($"    -- {OneLine(note)}");
            text.AppendLine($"    {items[i].Definition}{(i < items.Count - 1 ? "," : "")}");
        }

        text.AppendLine($")");
        text.AppendLine($"WITH (MEMORY_OPTIMIZED = ON, DURABILITY = {durability});");
    }

    private static (List<string> Notes, string Definition) Column(TableInfo table, ColumnInfo column, bool isIndexed, bool inPrimaryKey, string databaseCollation)
    {
        var name = Quote(column.Name);
        var notes = new List<string>();

        if (column.IsComputed)
        {
            var persisted = column.IsPersisted || isIndexed;
            if (persisted && !column.IsPersisted)
                notes.Add($"FIXED: {name} is indexed, so it becomes PERSISTED");
            var changed = TableRules.LossyColumnsIn(table, column.ComputedDefinition!);
            if (changed.Count > 0)
                notes.Add($"LOSSY: it uses {string.Join(", ", changed)}, whose type changes, so its values can change");
            return (notes, $"{name} AS {column.ComputedDefinition}{(persisted ? " PERSISTED" : "")}");
        }

        var mapping = TypeMap.Map(column);
        if (mapping.Rule is not null)
        {
            notes.Add($"{(mapping.Outcome == Outcome.Lossy ? "LOSSY" : "FIXED")}: {mapping.Source} → {mapping.Target}"
                + (mapping.Reason is null ? "" : $": {mapping.Reason}"));
        }

        if (column.IsSparse)
            notes.Add("FIXED: no longer SPARSE");

        // xml, json and vector have no collation: as text they take the source database's, even
        // in a target database that exists with another
        var collation = column.Collation ?? (databaseCollation.Length > 0 ? databaseCollation : null);
        var definition = new StringBuilder($"{name} {mapping.Target}");
        if (collation is not null && IsText(mapping.Target))
            definition.Append($" COLLATE {collation}");

        if (column.IsIdentity)
        {
            if (column.IdentitySeed != "1" || column.IdentityIncrement != "1")
                notes.Add($"LOSSY: IDENTITY({column.IdentitySeed},{column.IdentityIncrement}) becomes IDENTITY(1,1): existing values are kept, and new ones count up by 1 from the highest");
            definition.Append(" IDENTITY(1, 1)");
        }

        // A rowversion was filled in by SQL Server; as binary(8), nothing fills it in, unless it is
        // part of the primary key, which can't be NULL
        var nullable = column.IsNullable || (column.Type == "timestamp" && !inPrimaryKey);
        definition.Append(nullable ? " NULL" : " NOT NULL");

        if (column.DefaultDefinition is not null)
        {
            var unsupported = Expressions.UnsupportedInValue(column.DefaultDefinition);
            if (unsupported.Count > 0)
                notes.Add($"LOSSY: the default {column.DefaultDefinition} uses {Expressions.Describe(unsupported)}, and is left out");
            else
                definition.Append($" CONSTRAINT {Quote(column.DefaultName!)} DEFAULT {column.DefaultDefinition}");
        }

        return (notes, definition.ToString());
    }

    private static void Index(TableInfo table, IndexInfo index, bool schemaOnly, List<string> header, List<(List<string> Notes, string Definition)> items)
    {
        var name = Quote(index.Name);
        if (index.IsDisabled)
        {
            header.Add($"FIXED: index {name} is disabled, and isn't carried over");
            return;
        }

        switch (index.Kind)
        {
            case "XML" or "SPATIAL":
                header.Add($"FIXED: {index.Kind.ToLowerInvariant()} index {name} is left out");
                return;
            case "NONCLUSTERED COLUMNSTORE":
                header.Add($"FIXED: nonclustered columnstore index {name} is left out: a memory-optimized table can have a clustered one");
                return;
            case "CLUSTERED COLUMNSTORE":
                if ((schemaOnly ? "the table is SCHEMA_ONLY, which a columnstore index can't be" : Limits.ColumnstoreProblem(table)) is { } problem)
                    header.Add($"FIXED: clustered columnstore index {name} is left out: {problem}");
                else
                    items.Add(([], $"INDEX {name} CLUSTERED COLUMNSTORE"));
                return;
        }

        var keyBytes = Limits.KeyBytes(table, index);
        if (keyBytes > Limits.MaxKeyBytes)
        {
            header.Add(index.IsUnique
                ? $"LOSSY: unique index {name}'s key takes {Bytes(keyBytes)}, more than a range index's {Limits.MaxKeyBytes}: it is left out, and its uniqueness isn't enforced"
                : $"FIXED: index {name}'s key takes {Bytes(keyBytes)}, more than a range index's {Limits.MaxKeyBytes}: it is left out");
            return;
        }

        var notes = new List<string>();
        if (index.Kind == "CLUSTERED")
            notes.Add("FIXED: clustered, and becomes nonclustered");
        if (index.Filter is not null)
            notes.Add($"FIXED: its filter WHERE {index.Filter} is dropped, and the index covers every row");
        if (index.Included.Count > 0)
            notes.Add($"FIXED: INCLUDE ({string.Join(", ", index.Included.Select(Quote))}) is dropped: every index covers all columns");

        var columns = string.Join(", ", index.Keys.Select(Key));
        items.Add((notes, index switch
        {
            { IsUniqueConstraint: true } => $"CONSTRAINT {name} UNIQUE NONCLUSTERED ({columns})",
            { IsUnique: true } => $"INDEX {name} UNIQUE NONCLUSTERED ({columns})",
            _ => $"INDEX {name} NONCLUSTERED ({columns})",
        }));
    }

    private static void ForeignKey(StringBuilder text, ForeignKeyInfo key)
    {
        var actions = new[] { ("ON DELETE", key.DeleteAction), ("ON UPDATE", key.UpdateAction) }
            .Where(a => a.Item2 != "NO_ACTION")
            .Select(a => $"{a.Item1} {a.Item2.Replace('_', ' ')}")
            .ToList();
        if (actions.Count > 0)
            text.AppendLine($"-- LOSSY: {string.Join(" ", actions)} becomes NO ACTION");

        text.AppendLine($"ALTER TABLE {key.Table} ADD CONSTRAINT {Quote(key.Name)} FOREIGN KEY ({string.Join(", ", key.Columns.Select(c => Quote(c.Column)))})");
        text.AppendLine($"    REFERENCES {key.Referenced} ({string.Join(", ", key.Columns.Select(c => Quote(c.Referenced)))});");
    }

    private static string Bytes(int bytes) => bytes == int.MaxValue ? "a max column" : $"{bytes} bytes";

    private static string Key(IndexColumn column) => column.IsDescending ? $"{Quote(column.Name)} DESC" : Quote(column.Name);

    private static bool IsText(string type) =>
        type.StartsWith("char", StringComparison.Ordinal) || type.StartsWith("varchar", StringComparison.Ordinal)
        || type.StartsWith("nchar", StringComparison.Ordinal) || type.StartsWith("nvarchar", StringComparison.Ordinal)
        || type == "sysname";

    private static string Quote(string name) => ObjectName.Quote(name);

    private static string Literal(string value) => $"N'{value.Replace("'", "''")}'";

    // A definition can span lines; a comment can't
    private static string OneLine(string note) => note.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
}
