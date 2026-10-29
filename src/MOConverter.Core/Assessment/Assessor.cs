using Microsoft.Data.SqlClient;

using MOConverter.Catalog;
using MOConverter.Rules;

namespace MOConverter.Assessment;

public sealed record TableAssessment(ObjectName Name, long Rows, long EstimatedBytes, IReadOnlyList<Finding> Findings)
{
    public Outcome Outcome => Findings.Count == 0 ? Outcome.Ready : Findings.Max(f => f.Outcome);
}

/// <param name="Notes">What applies to the server or the selection rather than to one table.</param>
/// <param name="AlreadyMemoryOptimized">Selected tables that are memory-optimized already, and so left out.</param>
public sealed record Assessment(
    ServerFacts Server,
    IReadOnlyList<Finding> Notes,
    IReadOnlyList<TableAssessment> Tables,
    IReadOnlyList<ObjectName> AlreadyMemoryOptimized)
{
    public Outcome Outcome =>
        Notes.Select(n => n.Outcome).Concat(Tables.Select(t => t.Outcome)).DefaultIfEmpty(Outcome.Ready).Max();

    public long EstimatedBytes => Tables.Where(t => t.Outcome != Outcome.Blocked).Sum(t => t.EstimatedBytes);
}

/// <summary>Says which tables can become memory-optimized, and why the others can't. It changes nothing.</summary>
public static class Assessor
{
    public const int MinimumMajorVersion = 14;

    public static async Task<Assessment> AssessAsync(string connectionString, Selection selection, CancellationToken cancellationToken = default) =>
        Assess(await ReadAsync(connectionString, cancellationToken), selection);

    public static async Task<DatabaseModel> ReadAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        return await CatalogReader.ReadAsync(connection, cancellationToken);
    }

    public static Assessment Assess(DatabaseModel model, Selection selection)
    {
        var notes = ServerNotes(model.Server).ToList();
        var names = model.Tables.Select(t => t.Name).ToList();
        notes.AddRange(selection.Unmatched(names)
            .Select(name => new Finding(RuleCatalogue.SelectionUnmatched, $"no {name} in {ObjectName.Quote(model.Server.Database)}")));

        var chosen = selection.Resolve(names);
        var selected = model.Tables.Where(t => chosen.Contains(t.Name)).ToList();
        var findings = selected.Where(t => !t.IsMemoryOptimized)
            .ToDictionary(t => t.Name, t => TableRules.Check(t, model.ForeignKeys).ToList());
        var estimates = selected.ToDictionary(t => t.Name, EstimateBytes);

        if (model.Server.MemoryLimitBytes is { } limit)
        {
            foreach (var (name, list) in findings)
            {
                if (estimates[name] > limit)
                    list.Add(new Finding(RuleCatalogue.Memory, $"it needs about {Size(estimates[name])}, more than the {Size(limit)} available"));
            }
        }

        BlockAcrossBoundary(model, findings);

        var tables = findings.Select(f => new TableAssessment(f.Key, model.Tables.First(t => t.Name == f.Key).Rows, estimates[f.Key], f.Value))
            .ToList();

        var total = tables.Where(t => t.Outcome != Outcome.Blocked).Sum(t => t.EstimatedBytes);
        if (model.Server.MemoryLimitBytes is { } available && total > available)
            notes.Add(new Finding(RuleCatalogue.MemoryTotal, $"the tables that can be converted need about {Size(total)} together, more than the {Size(available)} available"));

        return new Assessment(model.Server, notes, tables,
            selected.Where(t => t.IsMemoryOptimized).Select(t => t.Name).ToList());
    }

    private static IEnumerable<Finding> ServerNotes(ServerFacts server)
    {
        if (!server.IsAzure && server.MajorVersion < MinimumMajorVersion)
            yield return new(RuleCatalogue.ServerVersion, $"this is SQL Server {server.ProductVersion}");
        if (!server.XtpSupported)
            yield return new(RuleCatalogue.ServerXtp, server.IsAzureSqlDatabase
                ? $"the service objective {server.ServiceObjective} has no In-Memory OLTP: only Premium and Business Critical do"
                : $"{server.Edition} doesn't support In-Memory OLTP");
        if (server.IsAzureSqlDatabase && server.XtpSupported)
            yield return new(RuleCatalogue.AzureSqlDatabase, $"service objective {server.ServiceObjective}: this tool hasn't been tested on Azure SQL Database");

        var edition = server.Edition.Split(' ')[0];
        if (!server.IsAzure && edition is "Express" or "Web" or "Standard")
            yield return new(RuleCatalogue.EditionQuota, $"{edition} edition caps memory-optimized data per database, below the server's memory");

        yield return new(RuleCatalogue.MemoryNote, server.MemoryLimitBytes is { } limit
            ? $"memory checked against {Size(limit)}: {server.MemoryLimitSource}"
            : $"memory {server.MemoryLimitSource}");
    }

    /// <summary>
    /// A foreign key can't link a memory-optimized table to a disk-based one, nor reach into
    /// another database. A table linked to one that isn't selected, that is blocked, or that is
    /// memory-optimized already (and so isn't copied into the new database) is blocked too;
    /// and so on, until no more change.
    /// </summary>
    private static void BlockAcrossBoundary(DatabaseModel model, Dictionary<ObjectName, List<Finding>> findings)
    {
        var memoryOptimized = model.Tables.Where(t => t.IsMemoryOptimized).Select(t => t.Name).ToHashSet();
        bool Converts(ObjectName name) =>
            findings.TryGetValue(name, out var list) && list.All(f => f.Outcome != Outcome.Blocked);

        var keys = model.ForeignKeys.Where(k => !k.IsDisabled && k.Table != k.Referenced).ToList();
        bool changed;
        do
        {
            changed = false;
            foreach (var key in keys)
            {
                foreach (var (side, other) in new[] { (key.Table, key.Referenced), (key.Referenced, key.Table) })
                {
                    if (!findings.TryGetValue(side, out var list) || !Converts(side) || Converts(other))
                        continue;

                    var why = memoryOptimized.Contains(other) ? "which is memory-optimized already, and isn't copied into the new database"
                        : findings.ContainsKey(other) ? "which can't be converted"
                        : "which isn't selected";
                    list.Add(new Finding(RuleCatalogue.ForeignKeyBoundary,
                        $"foreign key {ObjectName.Quote(key.Name)} links it to {other}, {why}"));
                    changed = true;
                }
            }
        }
        while (changed);
    }

    /// <summary>
    /// A guide, not a promise: each row takes its data, a 24-byte header and an 8-byte pointer
    /// per index, and Microsoft's sizing advice doubles that for row versions and growth.
    /// </summary>
    private static long EstimateBytes(TableInfo table)
    {
        var indexes = Math.Max(1, table.Indexes.Count(i => !i.IsDisabled && i.Kind is "CLUSTERED" or "NONCLUSTERED"));
        return 2 * (table.DataBytes + table.Rows * (24 + 8L * indexes));
    }

    public static string Size(long bytes) => bytes switch
    {
        >= 1L << 30 => FormattableString.Invariant($"{bytes / (double)(1L << 30):0.#} GB"),
        >= 1L << 20 => FormattableString.Invariant($"{bytes / (double)(1L << 20):0.#} MB"),
        >= 1L << 10 => FormattableString.Invariant($"{bytes / (double)(1L << 10):0.#} KB"),
        _ => FormattableString.Invariant($"{bytes} bytes"),
    };
}
