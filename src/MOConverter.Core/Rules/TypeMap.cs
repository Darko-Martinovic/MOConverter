using MOConverter.Catalog;

namespace MOConverter.Rules;

/// <summary>The type a column gets in the memory-optimized table.</summary>
/// <param name="Rule">Null when the column keeps its type.</param>
/// <param name="Source">The column's type as declared, e.g. <c>dbo.BigId</c> or <c>datetimeoffset(3)</c>.</param>
/// <param name="Target">The type the column gets, e.g. <c>bigint</c> or <c>datetime2(3)</c>.</param>
/// <param name="Reason">What changes for the data or for queries, when something does.</param>
public sealed record TypeMapping(Rule? Rule, string Source, string Target, string? Reason)
{
    public Outcome Outcome => Rule?.Outcome ?? Outcome.Ready;
}

/// <summary>
/// Maps column types to the types memory-optimized tables accept. Version 1 matched type names
/// with regular expressions in the wrong order, so bigint became int and varchar became char;
/// this is an explicit lookup on the system type, keeping length, precision and scale.
/// </summary>
public static class TypeMap
{
    /// <summary>The system types memory-optimized tables accept as they are.</summary>
    public static IReadOnlySet<string> Supported { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "bit", "tinyint", "smallint", "int", "bigint", "decimal", "numeric", "money", "smallmoney",
        "float", "real", "datetime", "smalldatetime", "datetime2", "date", "time",
        "char", "varchar", "nchar", "nvarchar", "binary", "varbinary", "uniqueidentifier", "sysname",
    };

    public static TypeMapping Map(ColumnInfo column)
    {
        if (column.IsAliasType)
        {
            var source = $"{column.TypeSchema}.{column.Type}";
            var underlying = MapSystem(column with { Type = column.BaseType!, IsAliasType = false });
            return underlying.Outcome switch
            {
                Outcome.Ready or Outcome.Fixed => new TypeMapping(RuleCatalogue.AliasType, source, underlying.Target, $"the alias type's base type is {underlying.Source}"),
                _ => underlying with { Source = $"{source} ({underlying.Source})" },
            };
        }

        return MapSystem(column);
    }

    private static TypeMapping MapSystem(ColumnInfo column)
    {
        // A CLR type of your own may share a name with a system type
        if (column.IsClrType && !column.TypeSchema.Equals("sys", StringComparison.OrdinalIgnoreCase))
        {
            return new TypeMapping(RuleCatalogue.TypeUnsupported, $"{column.TypeSchema}.{column.Type}", "",
                "a CLR type has no memory-optimized equivalent");
        }

        var source = Format(column.Type, column.MaxLength, column.Precision, column.Scale);
        if (Supported.Contains(column.Type))
            return new TypeMapping(null, source, source, null);

        return column.Type.ToLowerInvariant() switch
        {
            "text" => Fixed("varchar(max)"),
            "ntext" => Fixed("nvarchar(max)"),
            "image" => Fixed("varbinary(max)"),
            "xml" => Lossy("nvarchar(max)", "XML methods, XML indexes and schema validation are lost"),
            "geography" or "geometry" => Lossy("varbinary(max)", $"spatial methods are lost: CAST it back to {column.Type} to use them"),
            "hierarchyid" => Lossy("varbinary(892)", "hierarchyid methods are lost: CAST it back to hierarchyid to use them"),
            "datetimeoffset" => Lossy(Format("datetime2", 8, 0, column.Scale), "it is stored in UTC, and the offset is lost"),
            "timestamp" => Lossy("binary(8)", "rowversion no longer changes when the row changes"),
            "json" => Lossy("nvarchar(max)", "the json type's validation is lost"),
            "vector" => Lossy("nvarchar(max)", $"it is stored as a JSON array: CAST it back to vector({VectorDimensions(column)}) to use vector functions"),
            "sql_variant" => new TypeMapping(RuleCatalogue.TypeUnsupported, source, "", "sql_variant keeps a base type per value, and no memory-optimized type can"),
            _ => new TypeMapping(RuleCatalogue.TypeUnsupported, source, "", "not a type memory-optimized tables are known to accept"),
        };

        TypeMapping Fixed(string target) => new(RuleCatalogue.OldLobType, source, target, null);
        TypeMapping Lossy(string target, string reason) => new(RuleCatalogue.TypeLossy, source, target, reason);
    }

    /// <summary>A type as T-SQL declares it, from the lengths sys.columns gives.</summary>
    public static string Format(string type, int maxLength, int precision, int scale)
    {
        var name = type.ToLowerInvariant();
        return name switch
        {
            "decimal" or "numeric" => $"{name}({precision},{scale})",
            "datetime2" or "time" or "datetimeoffset" => $"{name}({scale})",
            "char" or "varchar" or "binary" or "varbinary" => $"{name}({Length(maxLength)})",
            "nchar" or "nvarchar" => $"{name}({(maxLength == -1 ? "max" : (maxLength / 2).ToString())})",
            "vector" => $"vector({(maxLength - 8) / 4})",
            "timestamp" => "rowversion",
            _ => name,
        };

        static string Length(int bytes) => bytes == -1 ? "max" : bytes.ToString();
    }

    // A vector stores an 8-byte header and 4 bytes per dimension
    private static int VectorDimensions(ColumnInfo column) => (column.MaxLength - 8) / 4;
}
