using MOConverter.Catalog;

namespace MOConverter.Rules;

/// <summary>
/// Size limits of memory-optimized tables that the assessment and the scripter both apply, so
/// that what one says the other does. SQL Server doesn't document how it counts, so these are
/// upper bounds, measured at the boundary for many shapes on 2019, 2022 and 2025:
/// ServerAgreesTests checks that the server takes everything they allow, and that they aren't
/// much stricter than it is. Near a limit, the tool may leave out an index the server would have
/// taken; it never keeps one the server refuses.
/// </summary>
public static class Limits
{
    /// <summary>The most a range (nonclustered) index's key can take.</summary>
    public const int MaxKeyBytes = 2500;

    /// <summary>The most a hash index's key can take: the in-row size of a row.</summary>
    public const int MaxHashKeyBytes = 8060;

    /// <summary>
    /// The most a row of a table with a clustered columnstore index can take, as RowBytes counts
    /// it: the 8060 bytes of a row, less 20 the columnstore index keeps.
    /// </summary>
    public const int MaxColumnstoreRowBytes = 8040;

    /// <summary>
    /// An index key's size: 2 bytes; each fixed-size column at what its key slot can take (its
    /// stored size, with short types and 8-byte ones padded); each variable column, char and
    /// binary included, with 2 bytes; 4 bytes if any column is nullable, and 4 if there's more
    /// than one column. int.MaxValue when a column is max.
    /// </summary>
    public static int KeyBytes(TableInfo table, IndexInfo index)
    {
        var columns = index.Keys.Select(k => table.Columns.Single(c => c.Name == k.Name)).ToList();
        if (columns.Any(c => ColumnBytes(c) < 0))
            return int.MaxValue;

        return 2
            + columns.Sum(c => IsVariable(c) ? ColumnBytes(c) + 2 : StoredBytes(c) switch { <= 4 => 4, 8 => 12, var s => s + 4 })
            + (columns.Any(c => IsNullable(table, c)) ? 4 : 0)
            + (columns.Count > 1 ? 4 : 0);
    }

    /// <summary>
    /// A row's size for the columnstore limit: each fixed-size column as it is stored, each
    /// variable one with 2 bytes, and 2 bytes for each 8 nullable columns. Computed columns
    /// aren't counted; a table with one can't keep its columnstore index anyway.
    /// </summary>
    public static int RowBytes(TableInfo table)
    {
        var columns = table.Columns.Where(c => !c.IsComputed).ToList();
        var nullable = columns.Count(c => IsNullable(table, c));
        return columns.Sum(c => IsVariable(c) ? ColumnBytes(c) + 2 : StoredBytes(c))
            + 2 * ((nullable + 7) / 8);
    }

    /// <summary>The most the in-row part of a row can take.</summary>
    public const int MaxInRowBytes = 8060;

    /// <summary>
    /// What must stay in the row, against 8060 bytes: 8 bytes; every fixed-size column as it is
    /// stored; every variable column of an index key whole, with 2 bytes; any other variable
    /// column whole with 2 bytes (at least 7) up to 24 bytes, and 8 bytes beyond, as SQL Server
    /// moves those off-row; and 2 bytes for each 8 nullable columns. Measured at the boundary, and
    /// checked by ServerAgreesTests.
    /// </summary>
    public static int InRowBytes(TableInfo table)
    {
        var keyed = KeptIndexes(table).SelectMany(i => i.Keys).Select(k => k.Name).ToHashSet(StringComparer.Ordinal);
        var columns = table.Columns.Where(c => !c.IsComputed || c.IsPersisted || keyed.Contains(c.Name)).ToList();
        var nullable = columns.Count(c => IsNullable(table, c));
        return 8
            + columns.Sum(c =>
            {
                if (!IsVariable(c))
                    return StoredBytes(c);
                var bytes = ColumnBytes(c);
                if (keyed.Contains(c.Name))
                    return bytes + 2;
                return bytes is >= 0 and <= 24 ? Math.Max(bytes + 2, 7) : 8;
            })
            + 2 * ((nullable + 7) / 8);
    }

    /// <summary>The indexes the memory-optimized table keeps whose keys must stay in the row.</summary>
    public static IEnumerable<IndexInfo> KeptIndexes(TableInfo table) =>
        table.Indexes.Where(i => !i.IsDisabled && i.Kind is "CLUSTERED" or "NONCLUSTERED"
            && (i.IsPrimaryKey || KeyBytes(table, i) <= MaxKeyBytes));

    /// <summary>
    /// Whether the memory-optimized column allows NULL: as in the source, but for a rowversion
    /// outside the primary key, whose binary(8) nothing fills in.
    /// </summary>
    public static bool IsNullable(TableInfo table, ColumnInfo column) =>
        column.IsNullable
        || (column.Type == "timestamp" && !table.Indexes.Any(i => i.IsPrimaryKey && i.Keys.Any(k => k.Name == column.Name)));

    private static bool IsVariable(ColumnInfo column) =>
        TypeMap.Map(column).Target.Split('(')[0] is "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary" or "sysname";

    /// <summary>
    /// What a fixed-size column takes in a memory-optimized row, which isn't always its size on
    /// disk: time, every datetime2 and a decimal of up to 18 digits take 8 bytes, a longer
    /// decimal 16, date 4.
    /// </summary>
    private static int StoredBytes(ColumnInfo column)
    {
        var target = TypeMap.Map(column).Target;
        var type = target.Split('(')[0];
        return type switch
        {
            "tinyint" or "bit" => 1,
            "smallint" => 2,
            "int" or "real" or "smallmoney" or "date" or "smalldatetime" => 4,
            "bigint" or "money" or "datetime" or "datetime2" or "time" => 8,
            "float" => column.MaxLength,
            "decimal" or "numeric" => column.Precision <= 18 ? 8 : 16,
            "uniqueidentifier" => 16,
            _ => Math.Max(column.MaxLength, 8),
        };
    }

    /// <summary>The most bytes a column takes in the memory-optimized table: -1 for max.</summary>
    public static int ColumnBytes(ColumnInfo column)
    {
        var target = TypeMap.Map(column).Target;
        if (target.EndsWith("(max)", StringComparison.Ordinal))
            return -1;

        var open = target.IndexOf('(');
        if (open > 0 && target[..open] is "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary")
        {
            var length = int.Parse(target[(open + 1)..^1], System.Globalization.CultureInfo.InvariantCulture);
            return target[..open].StartsWith('n') ? 2 * length : length;
        }

        return IsVariable(column) ? column.MaxLength : StoredBytes(column);
    }

    /// <summary>Why the table's clustered columnstore index can't be kept, or null when it can.</summary>
    public static string? ColumnstoreProblem(TableInfo table)
    {
        var max = table.Columns.FirstOrDefault(c => !c.IsComputed && ColumnBytes(c) < 0);
        if (max is not null)
            return $"{ObjectName.Quote(max.Name)} is {TypeMap.Map(max).Target}, which a columnstore index can't hold";

        var computed = table.Columns.FirstOrDefault(c => c.IsComputed);
        if (computed is not null)
            return $"the table has a computed column, {ObjectName.Quote(computed.Name)}";

        var row = RowBytes(table);
        return row > MaxColumnstoreRowBytes ? $"a row can take {row} bytes, more than the {MaxColumnstoreRowBytes} a columnstore index allows" : null;
    }
}
