using System.Diagnostics;
using System.Text.RegularExpressions;

using Microsoft.Data.SqlClient;

using MOConverter.Catalog;
using MOConverter.Rules;
using MOConverter.Scripting;

namespace MOConverter.Conversion;

/// <summary>A table's data, copied.</summary>
public sealed record TableCopied(ObjectName Table, long Rows, TimeSpan Took);

/// <summary>What stopped a conversion, and where.</summary>
public sealed class ConversionException(string step, Exception inner)
    : Exception($"{step}: {inner.Message}", inner)
{
    public string Step { get; } = step;
}

/// <summary>
/// Converts tables into a new database: runs the script up to the empty tables, copies each
/// table's rows with SqlBulkCopy, checks the counts, then adds the foreign keys. The data has to
/// pass through the client: a statement that touches a memory-optimized table can't read another
/// database (Msg 41317). The source is only read.
/// </summary>
public static partial class Converter
{
    /// <param name="target">A connection to the server the new database goes on; its database is ignored.</param>
    public static async Task<IReadOnlyList<TableCopied>> ConvertAsync(
        DatabaseModel model,
        IReadOnlyCollection<ObjectName> tables,
        ScriptOptions options,
        string source,
        string target,
        int batchSize,
        IProgress<TableCopied>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var (setup, foreignKeys) = Scripter.Parts(model, tables, options);
        var onServer = new SqlConnectionStringBuilder(target) { InitialCatalog = "master" }.ConnectionString;
        var inTarget = new SqlConnectionStringBuilder(target) { InitialCatalog = options.TargetDatabase }.ConnectionString;

        await Step("Creating the tables", () => RunAsync(onServer, setup, cancellationToken));

        var copied = new List<TableCopied>();
        foreach (var table in model.Tables.Where(t => tables.Contains(t.Name)))
        {
            var done = await Step($"Copying {table.Name}", () => CopyAsync(table, source, inTarget, batchSize, cancellationToken));
            copied.Add(done);
            progress?.Report(done);
        }

        if (foreignKeys.Length > 0)
            await Step("Adding the foreign keys", () => RunAsync(inTarget, foreignKeys, cancellationToken));

        return copied;
    }

    /// <summary>
    /// The SELECT that reads a table's rows as the memory-optimized table stores them. Computed
    /// columns are left out: the new table computes them.
    /// </summary>
    public static string SourceQuery(TableInfo table) =>
        $"SELECT {string.Join(", ", Copied(table).Select(Read))} FROM {table.Name};";

    private static IEnumerable<ColumnInfo> Copied(TableInfo table) => table.Columns.Where(c => !c.IsComputed);

    private static string Read(ColumnInfo column)
    {
        var name = ObjectName.Quote(column.Name);
        var mapping = TypeMap.Map(column);
        if (mapping.Rule is null || mapping.Rule == RuleCatalogue.AliasType)
            return name;

        var type = column.IsAliasType ? column.BaseType : column.Type;
        // The UTC time, so the instant survives even though the offset doesn't
        return type == "datetimeoffset"
            ? $"CAST(SWITCHOFFSET({name}, '+00:00') AS {mapping.Target}) AS {name}"
            : $"CAST({name} AS {mapping.Target}) AS {name}";
    }

    private static async Task<TableCopied> CopyAsync(TableInfo table, string source, string target, int batchSize, CancellationToken cancellationToken)
    {
        var clock = Stopwatch.StartNew();
        await using (var from = new SqlConnection(source))
        {
            await from.OpenAsync(cancellationToken);
            await using var command = new SqlCommand(SourceQuery(table), from) { CommandTimeout = 0 };
            await using var reader = await command.ExecuteReaderAsync(System.Data.CommandBehavior.SequentialAccess, cancellationToken);

            // KeepIdentity keeps the values; KeepNulls keeps a NULL where the column has a default
            using var bulk = new SqlBulkCopy(target,
                SqlBulkCopyOptions.KeepIdentity | SqlBulkCopyOptions.KeepNulls | SqlBulkCopyOptions.CheckConstraints)
            {
                DestinationTableName = table.Name.ToString(),
                BatchSize = batchSize,
                BulkCopyTimeout = 0,
                EnableStreaming = true,
            };
            foreach (var column in Copied(table))
                bulk.ColumnMappings.Add(column.Name, column.Name);
            await bulk.WriteToServerAsync(reader, cancellationToken);
        }

        var expected = await CountAsync(source, table.Name, cancellationToken);
        var rows = await CountAsync(target, table.Name, cancellationToken);
        if (rows != expected)
            throw new InvalidOperationException($"{expected} rows read, but {rows} in the new table");

        return new TableCopied(table.Name, rows, clock.Elapsed);
    }

    private static async Task<long> CountAsync(string connectionString, ObjectName table, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand($"SELECT COUNT_BIG(*) FROM {table};", connection) { CommandTimeout = 0 };
        return (long)(await command.ExecuteScalarAsync(cancellationToken))!;
    }

    /// <summary>Runs a script's batches on one connection, as sqlcmd does, so its USE carries over.</summary>
    private static async Task RunAsync(string connectionString, string script, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        foreach (var batch in Go().Split(script).Where(b => b.Trim().Length > 0))
        {
            await using var command = new SqlCommand(batch, connection) { CommandTimeout = 0 };
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task<T> Step<T>(string step, Func<Task<T>> work)
    {
        try
        {
            return await work();
        }
        catch (Exception e) when (e is SqlException or InvalidOperationException)
        {
            throw new ConversionException(step, e);
        }
    }

    private static Task Step(string step, Func<Task> work) => Step(step, async () =>
    {
        await work();
        return true;
    });

    [GeneratedRegex(@"^\s*GO\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase)]
    private static partial Regex Go();
}
