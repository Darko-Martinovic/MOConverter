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

    /// <summary>The longest row a table with a clustered columnstore index can have.</summary>
    public const int MaxColumnstoreRowBytes = 8060;

    /// <summary>
    /// A range index's key size as SQL Server counts it against 2500: the columns' sizes, 4
    /// bytes, and 2 more for each nullable column (found by trying the boundary on 2022 and 2025).
    /// </summary>
    public static int KeyBytes(TableInfo table, IndexInfo index)
    {
        var columns = index.Keys.Select(k => table.Columns.Single(c => c.Name == k.Name)).ToList();
        var sizes = columns.Select(ColumnBytes).ToList();
        return sizes.Any(s => s < 0) ? int.MaxValue : 4 + sizes.Sum() + 2 * columns.Count(c => c.IsNullable);
    }

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
