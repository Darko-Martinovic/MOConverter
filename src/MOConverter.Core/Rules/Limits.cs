using MOConverter.Catalog;

namespace MOConverter.Rules;

/// <summary>
/// Size limits of memory-optimized tables that the assessment and the scripter both apply, so
/// that what one says the other does. ServerAgreesTests checks the numbers.
/// </summary>
public static class Limits
{
    /// <summary>The most a range (nonclustered) index's key can take, overhead included.</summary>
    public const int MaxKeyBytes = 2500;

    /// <summary>
    /// The most the columns of a table with a clustered columnstore index can declare together: a
    /// row of memory-optimized table can take 8060 bytes, and the columnstore index keeps 20 of
    /// them (found at the boundary for several shapes, and checked by ServerAgreesTests).
    /// </summary>
    public const int MaxColumnstoreRowBytes = 8040;

    /// <summary>
    /// A range index's key size, as an upper bound of what SQL Server counts against 2500. It was
    /// measured at the boundary for many shapes on 2019, 2022 and 2025 (ServerAgreesTests checks
    /// it never counts less than the server, and at most a few bytes more). It is:
    /// 2 bytes; the fixed-size columns, rounded up to an even number; the variable ones, with 2
    /// bytes each (char and binary count as variable here); 4 bytes for each fixed column of 8
    /// bytes or more but uniqueidentifier; and 4 bytes if any column is nullable.
    /// </summary>
    public static int KeyBytes(TableInfo table, IndexInfo index)
    {
        var columns = index.Keys.Select(k => table.Columns.Single(c => c.Name == k.Name)).ToList();
        if (columns.Any(c => ColumnBytes(c) < 0))
            return int.MaxValue;

        var variable = columns.Where(IsVariable).ToList();
        var fixedSize = columns.Except(variable).ToList();
        var fixedBytes = fixedSize.Sum(ColumnBytes);
        return 2
            + (fixedBytes + 1) / 2 * 2
            + variable.Sum(c => ColumnBytes(c) + 2)
            + 4 * fixedSize.Count(c => ColumnBytes(c) >= 8 && TypeMap.Map(c).Target != "uniqueidentifier")
            + (columns.Any(c => IsNullable(table, c)) ? 4 : 0);
    }

    /// <summary>
    /// Whether the memory-optimized column allows NULL: as in the source, but for a rowversion
    /// outside the primary key, whose binary(8) nothing fills in.
    /// </summary>
    public static bool IsNullable(TableInfo table, ColumnInfo column) =>
        column.IsNullable
        || (column.Type == "timestamp" && !table.Indexes.Any(i => i.IsPrimaryKey && i.Keys.Any(k => k.Name == column.Name)));

    private static bool IsVariable(ColumnInfo column) =>
        TypeMap.Map(column).Target.Split('(')[0] is "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary" or "sysname";

    /// <summary>The most bytes a column takes in the memory-optimized table: -1 for max.</summary>
    public static int ColumnBytes(ColumnInfo column)
    {
        var target = TypeMap.Map(column).Target;
        if (target.EndsWith("(max)", StringComparison.Ordinal))
            return -1;
        if (target.StartsWith("datetime2(", StringComparison.Ordinal))
            return column.Scale switch { <= 2 => 6, <= 4 => 7, _ => 8 };

        var open = target.IndexOf('(');
        if (open > 0 && target[..open] is "char" or "varchar" or "nchar" or "nvarchar" or "binary" or "varbinary")
        {
            var length = int.Parse(target[(open + 1)..^1], System.Globalization.CultureInfo.InvariantCulture);
            return target[..open].StartsWith('n') ? 2 * length : length;
        }

        return column.MaxLength;
    }

    /// <summary>The range indexes, primary key included, that are kept: not disabled, not columnstore, XML or spatial.</summary>
    public static bool IsRangeIndex(IndexInfo index) =>
        !index.IsDisabled && index.Kind is "CLUSTERED" or "NONCLUSTERED";

    /// <summary>Why the table's clustered columnstore index can't be kept, or null when it can.</summary>
    public static string? ColumnstoreProblem(TableInfo table)
    {
        var max = table.Columns.FirstOrDefault(c => !c.IsComputed && ColumnBytes(c) < 0);
        if (max is not null)
            return $"{ObjectName.Quote(max.Name)} is {TypeMap.Map(max).Target}, which a columnstore index can't hold";

        var computed = table.Columns.FirstOrDefault(c => c.IsComputed);
        if (computed is not null)
            return $"the table has a computed column, {ObjectName.Quote(computed.Name)}";

        var row = table.Columns.Where(c => !c.IsComputed).Sum(ColumnBytes);
        return row > MaxColumnstoreRowBytes ? $"a row can take {row} bytes, more than {MaxColumnstoreRowBytes}" : null;
    }

    public static IndexInfo? Columnstore(TableInfo table) =>
        table.Indexes.FirstOrDefault(i => !i.IsDisabled && i.Kind == "CLUSTERED COLUMNSTORE");
}
