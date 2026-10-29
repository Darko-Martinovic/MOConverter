using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace MOConverter.Rules;

/// <summary>
/// Finds what a check constraint, default or computed column uses that memory-optimized tables
/// don't accept. The expression is parsed, so a column named UpperLimit isn't taken for UPPER,
/// as version 1's text search took it.
/// </summary>
public static class Expressions
{
    /// <summary>Built-in functions seen to work in memory-optimized tables (ServerAgreesTests checks each).</summary>
    public static IReadOnlySet<string> SupportedFunctions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "ABS", "CEILING", "FLOOR", "ROUND", "POWER", "SQRT", "SIGN",
        "LEN", "LTRIM", "RTRIM", "TRIM", "SUBSTRING", "CONCAT_WS", "TRANSLATE", "ISNULL",
        "DATEADD", "DATEDIFF", "DATEPART", "YEAR", "MONTH", "DAY", "EOMONTH", "DATEFROMPARTS",
        "GETDATE", "GETUTCDATE", "SYSDATETIME", "SYSUTCDATETIME",
        "ISJSON", "JSON_VALUE", "NEWID", "NEWSEQUENTIALID", "SUSER_SNAME",
    };

    /// <summary>What memory-optimized tables are seen to refuse (ServerAgreesTests checks each).</summary>
    public static IReadOnlySet<string> KnownUnsupported { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "LIKE", "NEXT VALUE FOR", "TRY_CAST", "TRY_CONVERT",
        "DATALENGTH", "LEFT", "RIGHT", "REPLACE", "REPLICATE", "REVERSE", "CONCAT", "LOWER", "UPPER",
        "CHARINDEX", "PATINDEX", "STUFF", "FORMAT", "ISNUMERIC", "HASHBYTES", "DB_NAME", "HOST_NAME", "APP_NAME",
    };

    /// <summary>The unsupported parts of a boolean expression, such as a check constraint's definition.</summary>
    public static IReadOnlyList<string> UnsupportedInCondition(string definition) => Find(definition, condition: true);

    /// <summary>The unsupported parts of a scalar expression, such as a default or a computed column.</summary>
    public static IReadOnlyList<string> UnsupportedInValue(string definition) => Find(definition, condition: false);

    /// <summary>Says what was found, telling apart what is known to fail from what isn't known to work.</summary>
    public static string Describe(IReadOnlyList<string> unsupported)
    {
        var known = unsupported.Where(u => KnownUnsupported.Contains(u)
            || u.StartsWith("CAST to ", StringComparison.Ordinal) || u.StartsWith("CONVERT to ", StringComparison.Ordinal)
            || u.EndsWith(" (a property)", StringComparison.Ordinal)).ToList();
        var unknown = unsupported.Except(known).ToList();
        var parts = new List<string>();
        if (known.Count > 0)
            parts.Add($"{string.Join(", ", known)}, which memory-optimized tables don't accept");
        if (unknown.Count > 0)
            parts.Add($"{string.Join(", ", unknown)}, which isn't known to work in memory-optimized tables");
        return string.Join("; and ", parts);
    }

    /// <summary>The columns a scalar expression reads, by name.</summary>
    public static IReadOnlySet<string> ColumnsIn(string definition)
    {
        var fragment = new TSql170Parser(initialQuotedIdentifiers: true).ParseExpression(new StringReader(definition), out var errors);
        var columns = new ColumnFinder();
        if (fragment is not null && errors.Count == 0)
            fragment.Accept(columns);
        return columns.Found;
    }

    private sealed class ColumnFinder : TSqlFragmentVisitor
    {
        public HashSet<string> Found { get; } = new(StringComparer.Ordinal);

        public override void Visit(ColumnReferenceExpression node)
        {
            if (node.MultiPartIdentifier is { Count: > 0 } name)
                Found.Add(name.Identifiers[^1].Value);
        }
    }

    private static List<string> Find(string definition, bool condition)
    {
        var parser = new TSql170Parser(initialQuotedIdentifiers: true);
        IList<ParseError> errors;
        TSqlFragment? fragment = condition
            ? parser.ParseBooleanExpression(new StringReader(definition), out errors)
            : parser.ParseExpression(new StringReader(definition), out errors);
        if (fragment is null || errors.Count > 0)
            return ["an expression this tool can't parse"];

        var finder = new Finder();
        fragment.Accept(finder);
        return finder.Found.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private sealed class Finder : TSqlFragmentVisitor
    {
        public List<string> Found { get; } = [];

        public override void Visit(FunctionCall node)
        {
            var name = node.FunctionName.Value.ToUpperInvariant();
            if (node.CallTarget is not null)
                Found.Add($"{name} (a user-defined function or a method)");
            else if (!SupportedFunctions.Contains(name))
                Found.Add(name);
        }

        // Place.Lat reads a property of a CLR column, which becomes varbinary; a check or computed
        // column names its own table's columns with one part
        public override void Visit(ColumnReferenceExpression node)
        {
            if (node.MultiPartIdentifier is { Count: >= 2 } name)
                Found.Add($"{string.Join(".", name.Identifiers.Select(i => i.Value))} (a property)");
        }

        public override void Visit(LeftFunctionCall node) => Found.Add("LEFT");

        public override void Visit(RightFunctionCall node) => Found.Add("RIGHT");

        public override void Visit(LikePredicate node) => Found.Add("LIKE");

        // CAST and CONVERT work, but not to a type memory-optimized tables refuse
        public override void Visit(CastCall node) => Cast("CAST", node.DataType);

        public override void Visit(ConvertCall node) => Cast("CONVERT", node.DataType);

        private void Cast(string call, DataTypeReference type)
        {
            var name = type.Name.BaseIdentifier.Value;
            if (!TypeMap.Supported.Contains(name))
                Found.Add($"{call} to {name.ToLowerInvariant()}");
        }

        public override void Visit(TryCastCall node) => Found.Add("TRY_CAST");

        public override void Visit(TryConvertCall node) => Found.Add("TRY_CONVERT");

        public override void Visit(ParseCall node) => Found.Add("PARSE");

        public override void Visit(TryParseCall node) => Found.Add("TRY_PARSE");

        public override void Visit(NextValueForExpression node) => Found.Add("NEXT VALUE FOR");

        public override void Visit(AtTimeZoneCall node) => Found.Add("AT TIME ZONE");

        public override void Visit(ScalarSubquery node) => Found.Add("a subquery");

        public override void Visit(GlobalVariableExpression node) => Found.Add(node.Name.ToUpperInvariant());

        public override void Visit(ParameterlessCall node)
        {
            if (node.ParameterlessCallType is not (ParameterlessCallType.CurrentTimestamp or ParameterlessCallType.CurrentUser))
                Found.Add(node.ParameterlessCallType.ToString().ToUpperInvariant());
        }
    }
}
