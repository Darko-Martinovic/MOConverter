using System.Text;

using MOConverter.Catalog;

namespace MOConverter.Assessment;

/// <summary>A table named on the command line: <c>schema.table</c>, or a bare name for any schema.</summary>
public sealed record SelectedTable(string? Schema, string Name)
{
    public bool Matches(ObjectName table, StringComparison comparison) =>
        string.Equals(Name, table.Name, comparison)
        && (Schema is null || string.Equals(Schema, table.Schema, comparison));

    public override string ToString() => Schema is null ? ObjectName.Quote(Name) : $"{ObjectName.Quote(Schema)}.{ObjectName.Quote(Name)}";
}

/// <summary>Which tables to look at: those in the listed schemas and the listed tables, or every table when both are empty.</summary>
public sealed record Selection(IReadOnlyList<string> Schemas, IReadOnlyList<SelectedTable> Tables)
{
    public static Selection All { get; } = new([], []);

    public bool IsAll => Schemas.Count == 0 && Tables.Count == 0;

    /// <summary>
    /// The tables the selection names. A name matches without regard to case, unless some table
    /// matches it exactly, as in a case-sensitive database with both dbo.Orders and dbo.orders.
    /// </summary>
    public IReadOnlySet<ObjectName> Resolve(IReadOnlyCollection<ObjectName> tables)
    {
        if (IsAll)
            return tables.ToHashSet();

        return Schemas.SelectMany(s => Matching(tables, (t, c) => string.Equals(s, t.Schema, c)))
            .Concat(Tables.SelectMany(n => Matching(tables, (t, c) => n.Matches(t, c))))
            .ToHashSet();
    }

    /// <summary>The names in the selection that match no table.</summary>
    public IEnumerable<string> Unmatched(IReadOnlyCollection<ObjectName> tables) =>
        Schemas.Where(s => !Matching(tables, (t, c) => string.Equals(s, t.Schema, c)).Any())
            .Select(s => $"schema {ObjectName.Quote(s)}")
            .Concat(Tables.Where(n => !Matching(tables, (t, c) => n.Matches(t, c)).Any()).Select(n => $"table {n}"));

    private static IEnumerable<ObjectName> Matching(IReadOnlyCollection<ObjectName> tables, Func<ObjectName, StringComparison, bool> matches)
    {
        var exact = tables.Where(t => matches(t, StringComparison.Ordinal)).ToList();
        return exact.Count > 0 ? exact : tables.Where(t => matches(t, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Reads comma-separated lists. Names may be bracketed or double-quoted, so
    /// <c>[Sales].[Order Details]</c> and <c>"a,b"</c> work.
    /// </summary>
    public static Selection Parse(string? tables, string? schemas)
    {
        var schemaList = Split(schemas, ',')
            .Select(s => Single(s, "schema"))
            .ToList();
        var tableList = Split(tables, ',')
            .Select(item =>
            {
                var parts = Split(item, '.').ToList();
                return parts.Count switch
                {
                    1 => new SelectedTable(null, Unquote(parts[0])),
                    2 => new SelectedTable(Unquote(parts[0]), Unquote(parts[1])),
                    _ => throw new FormatException($"'{item}' isn't a table name: use schema.table or table"),
                };
            })
            .ToList();
        return new Selection(schemaList, tableList);
    }

    private static string Single(string item, string what)
    {
        var parts = Split(item, '.').ToList();
        return parts.Count == 1 ? Unquote(parts[0]) : throw new FormatException($"'{item}' isn't a {what} name");
    }

    /// <summary>Splits on a separator outside brackets and double quotes, dropping empty items.</summary>
    private static IEnumerable<string> Split(string? text, char separator)
    {
        if (string.IsNullOrWhiteSpace(text))
            yield break;

        var current = new StringBuilder();
        char? closing = null;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (closing is not null)
            {
                current.Append(c);
                if (c == closing)
                {
                    // ]] and "" are an escaped ] and "
                    if (i + 1 < text.Length && text[i + 1] == closing)
                        current.Append(text[++i]);
                    else
                        closing = null;
                }
            }
            else if (c == '[' || c == '"')
            {
                closing = c == '[' ? ']' : '"';
                current.Append(c);
            }
            else if (c == separator)
            {
                if (current.ToString().Trim().Length > 0)
                    yield return current.ToString().Trim();
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (closing is not null)
            throw new FormatException($"'{text}' has an unclosed {(closing == ']' ? "[" : "\"")}");
        if (current.ToString().Trim().Length > 0)
            yield return current.ToString().Trim();
    }

    private static string Unquote(string part)
    {
        part = part.Trim();
        if (part.Length >= 2 && part[0] == '[' && part[^1] == ']')
            return part[1..^1].Replace("]]", "]");
        if (part.Length >= 2 && part[0] == '"' && part[^1] == '"')
            return part[1..^1].Replace("\"\"", "\"");
        return part;
    }
}
