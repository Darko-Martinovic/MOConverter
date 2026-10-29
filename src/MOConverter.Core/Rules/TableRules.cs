using MOConverter.Catalog;

namespace MOConverter.Rules;

/// <summary>The rules that look at one table on its own. Foreign keys between tables are the assessor's.</summary>
public static class TableRules
{
    public static IEnumerable<Finding> Check(TableInfo table, IEnumerable<ForeignKeyInfo> foreignKeys) =>
        TableKind(table)
            .Concat(table.Columns.SelectMany(c => Column(table, c)))
            .Concat(Indexes(table))
            .Concat(table.Checks.SelectMany(c => Check(table.Name, c)))
            .Concat(foreignKeys.Where(k => k.Table == table.Name).SelectMany(ForeignKey))
            .Concat(table.Triggers.Select(Trigger))
            .Concat(RowLength(table));

    private static IEnumerable<Finding> RowLength(TableInfo table)
    {
        // Only for a table that has a primary key; one without is blocked already
        if (!table.Indexes.Any(i => i.IsPrimaryKey) || table.Columns.Any(c => !c.IsComputed && TypeMap.Map(c).Rule == RuleCatalogue.TypeUnsupported))
            yield break;

        var bytes = Limits.InRowBytes(table);
        if (bytes > Limits.MaxInRowBytes)
        {
            yield return new(RuleCatalogue.RowTooLong,
                $"what must stay in its rows takes {bytes} bytes, more than {Limits.MaxInRowBytes}: fixed-size columns and the variable ones in index keys can't move off-row");
        }
    }

    private static IEnumerable<Finding> TableKind(TableInfo table)
    {
        var primaryKey = table.Indexes.FirstOrDefault(i => i.IsPrimaryKey);
        if (primaryKey is null)
            yield return new(RuleCatalogue.NoPrimaryKey, "it has no primary key");
        else if (primaryKey.IsDisabled)
            yield return new(RuleCatalogue.NoPrimaryKey, $"its primary key {ObjectName.Quote(primaryKey.Name)} is disabled");
        if (table.TemporalType == 2)
            yield return new(RuleCatalogue.Temporal, "it is system-versioned");
        if (table.TemporalType == 1)
            yield return new(RuleCatalogue.Temporal, "it is the history table of a system-versioned table");
        if (table.LedgerType != 0)
            yield return new(RuleCatalogue.Ledger, "it is a ledger table");
        if (table.IsGraphTable)
            yield return new(RuleCatalogue.Graph, "it is a graph table");
        if (table.IsFileTable)
            yield return new(RuleCatalogue.FileTable, "it is a FileTable");
        if (table.IsReplicated)
            yield return new(RuleCatalogue.Replicated, "it is published for replication");
        if (table.IsChangeTracked)
            yield return new(RuleCatalogue.ChangeTracking, "change tracking is on, and will be lost");
        if (table.IsTrackedByCdc)
            yield return new(RuleCatalogue.ChangeDataCapture, "change data capture is on, and will be lost");
        if (table.HasFullTextIndex)
            yield return new(RuleCatalogue.FullText, "its full-text index will be lost");
        if (table.IsPartitioned)
            yield return new(RuleCatalogue.Partitioned, "it is partitioned, and becomes one partition");
        if (table.Compression is not null)
            yield return new(RuleCatalogue.Compression, $"its {table.Compression.ToLowerInvariant()} compression is dropped");
    }

    private static IEnumerable<Finding> Column(TableInfo table, ColumnInfo column)
    {
        var name = ObjectName.Quote(column.Name);

        if (column.IsColumnSet)
        {
            yield return new(RuleCatalogue.ColumnSet, $"{name} is a column set");
            yield break;
        }

        if (column.IsComputed)
        {
            if (column.ComputedDefinition is null)
            {
                yield return new(RuleCatalogue.DefinitionHidden, $"{name}'s computed definition isn't visible");
                yield break;
            }

            // The expression stays, so its result must be a type memory-optimized tables accept as it is
            var result = TypeMap.Map(column);
            var unsupported = Expressions.UnsupportedInValue(column.ComputedDefinition);
            if (unsupported.Count > 0)
            {
                yield return new(RuleCatalogue.ComputedUnsupported, $"{name} AS {column.ComputedDefinition} uses {Expressions.Describe(unsupported)}");
                yield break;
            }

            if (result.Rule is not null)
            {
                yield return new(RuleCatalogue.ComputedUnsupported, $"{name} AS {column.ComputedDefinition} gives {result.Source}, which memory-optimized tables don't accept as it is");
                yield break;
            }

            if (!column.IsPersisted && table.Indexes.Any(i => i.Keys.Any(k => k.Name == column.Name)))
                yield return new(RuleCatalogue.ComputedIndexPersisted, $"{name} is indexed, so it becomes PERSISTED");

            var changed = LossyColumnsIn(table, column.ComputedDefinition);
            if (changed.Count > 0)
                yield return new(RuleCatalogue.ComputedOverLossy, $"{name} AS {column.ComputedDefinition} uses {string.Join(", ", changed)}, whose type changes, so its values can change");
            yield break;
        }

        var mapping = TypeMap.Map(column);
        if (mapping.Rule is not null)
        {
            var change = mapping.Target.Length == 0 ? mapping.Source : $"{mapping.Source} → {mapping.Target}";
            yield return new(mapping.Rule, mapping.Reason is null ? $"{name} {change}" : $"{name} {change}: {mapping.Reason}");
        }

        if (column.IsSparse)
            yield return new(RuleCatalogue.Sparse, $"{name} is no longer SPARSE");

        if (column.IsIdentity && (column.IdentitySeed != "1" || column.IdentityIncrement != "1"))
        {
            yield return new(RuleCatalogue.IdentitySeed,
                $"{name} is IDENTITY({column.IdentitySeed},{column.IdentityIncrement}): it becomes IDENTITY(1,1), and new values count up by 1 from the highest");
        }

        if (column.DefaultName is not null && column.DefaultDefinition is null)
        {
            yield return new(RuleCatalogue.DefinitionHidden, $"{name}'s default {ObjectName.Quote(column.DefaultName)} isn't visible");
        }
        else if (column.DefaultDefinition is not null)
        {
            var unsupported = Expressions.UnsupportedInValue(column.DefaultDefinition);
            if (unsupported.Count > 0)
            {
                yield return new(RuleCatalogue.DefaultUnsupported,
                    $"{name}'s default {column.DefaultDefinition} uses {Expressions.Describe(unsupported)}, and is left out");
            }
        }
    }

    private static IEnumerable<Finding> Indexes(TableInfo table)
    {
        foreach (var index in table.Indexes)
        {
            var name = ObjectName.Quote(index.Name);
            if (index.IsDisabled)
            {
                yield return new(RuleCatalogue.DisabledObject, $"index {name} is disabled, and isn't carried over");
                continue;
            }

            switch (index.Kind)
            {
                case "XML" or "SPATIAL":
                    yield return new(RuleCatalogue.XmlOrSpatialIndex, $"{index.Kind.ToLowerInvariant()} index {name} is left out");
                    continue;
                case "NONCLUSTERED COLUMNSTORE":
                    yield return new(RuleCatalogue.NonclusteredColumnstore, $"nonclustered columnstore index {name} is left out");
                    continue;
                case "CLUSTERED COLUMNSTORE":
                    if (Limits.ColumnstoreProblem(table) is { } problem)
                        yield return new(RuleCatalogue.ColumnstoreLimits, $"clustered columnstore index {name} is left out: {problem}");
                    continue;
                case "CLUSTERED" when !index.IsPrimaryKey:
                    yield return new(RuleCatalogue.ClusteredIndex, $"clustered index {name} becomes nonclustered");
                    break;
            }

            var key = Limits.KeyBytes(table, index);
            if (key > Limits.MaxKeyBytes)
            {
                var wide = $"{(index.IsPrimaryKey ? "primary key" : "index")} {name}'s key takes {(key == int.MaxValue ? "a max column" : $"{key} bytes")}, more than a range index's {Limits.MaxKeyBytes}";
                if (index.IsPrimaryKey && key == int.MaxValue)
                {
                    // No index of a memory-optimized table can have a max column in its key, a hash one neither
                    yield return new(RuleCatalogue.MaxKey, $"primary key {name} has a column that becomes max, which no index can have in its key");
                }
                else if (index.IsPrimaryKey && key > Limits.MaxHashKeyBytes)
                {
                    yield return new(RuleCatalogue.MaxKey, $"{wide}, and more than the {Limits.MaxHashKeyBytes} a hash index allows");
                }
                else if (index.IsPrimaryKey)
                {
                    yield return new(RuleCatalogue.WideKey, $"{wide}: it becomes a hash index");
                }
                else
                {
                    yield return index.IsUnique
                        ? new(RuleCatalogue.WideUniqueKey, $"{wide}: it is left out, and its uniqueness isn't enforced")
                        : new(RuleCatalogue.WideKey, $"{wide}: it is left out");
                    continue;
                }
            }

            if (index.HasFilter && index.Filter is null)
                yield return new(RuleCatalogue.DefinitionHidden, $"index {name}'s filter isn't visible");
            else if (index.Filter is not null && index.IsUnique)
                yield return new(RuleCatalogue.UniqueFilteredIndex, $"unique index {name} is filtered WHERE {index.Filter}");
            else if (index.Filter is not null)
                yield return new(RuleCatalogue.FilteredIndex, $"index {name} loses its filter WHERE {index.Filter}");

            if (index.Included.Count > 0)
                yield return new(RuleCatalogue.IncludedColumns, $"index {name} drops INCLUDE ({string.Join(", ", index.Included.Select(ObjectName.Quote))})");
        }
    }

    /// <summary>The columns an expression reads whose conversion is lossy, quoted.</summary>
    public static IReadOnlyList<string> LossyColumnsIn(TableInfo table, string definition) =>
        Expressions.ColumnsIn(definition)
            .Select(n => table.Columns.FirstOrDefault(c => c.Name == n))
            .Where(c => c is { IsComputed: false } && TypeMap.Map(c).Outcome == Outcome.Lossy)
            .Select(c => ObjectName.Quote(c!.Name))
            .Order(StringComparer.Ordinal)
            .ToList();

    private static IEnumerable<Finding> Check(ObjectName table, CheckInfo check)
    {
        var name = ObjectName.Quote(check.Name);
        if (check.IsDisabled)
        {
            yield return new(RuleCatalogue.DisabledObject, $"check constraint {name} is disabled, and isn't carried over");
            yield break;
        }

        if (check.Definition.Length == 0)
        {
            yield return new(RuleCatalogue.DefinitionHidden, $"check constraint {name}'s definition isn't visible");
            yield break;
        }

        var unsupported = Expressions.UnsupportedInCondition(check.Definition);
        if (unsupported.Count > 0)
        {
            yield return new(RuleCatalogue.CheckUnsupported,
                $"check constraint {name} {check.Definition} uses {Expressions.Describe(unsupported)}, and is left out");
        }
        else if (check.IsNotForReplication)
        {
            yield return new(RuleCatalogue.Untrusted, NotForReplication("check constraint", table, check.Name));
        }
        else if (check.IsNotTrusted)
        {
            yield return new(RuleCatalogue.Untrusted, Untrusted("check constraint", table, check.Name));
        }
    }

    // SQL Server keeps such a constraint untrusted whatever the rows, and replication may have let
    // in rows that break it; memory-optimized tables don't have NOT FOR REPLICATION at all
    private static string NotForReplication(string what, ObjectName table, string name) =>
        $"{what} {ObjectName.Quote(name)} is NOT FOR REPLICATION, which memory-optimized tables don't support, and its rows aren't vouched for: "
        + $"check them with DBCC CHECKCONSTRAINTS ('{table.ToString().Replace("'", "''")}'), then recreate it without NOT FOR REPLICATION";

    private static string Untrusted(string what, ObjectName table, string name) =>
        $"{what} {ObjectName.Quote(name)} was made WITH NOCHECK, so rows may break it: check them with "
        + $"ALTER TABLE {table} WITH CHECK CHECK CONSTRAINT {ObjectName.Quote(name)}, or drop it";

    private static IEnumerable<Finding> ForeignKey(ForeignKeyInfo key)
    {
        var name = ObjectName.Quote(key.Name);
        if (key.IsDisabled)
        {
            yield return new(RuleCatalogue.DisabledObject, $"foreign key {name} is disabled, and isn't carried over");
            yield break;
        }

        if (!key.ReferencesPrimaryKey)
            yield return new(RuleCatalogue.ForeignKeyTarget, $"foreign key {name} references a unique key of {key.Referenced}, not its primary key");
        if (key.IsNotForReplication)
            yield return new(RuleCatalogue.Untrusted, NotForReplication("foreign key", key.Table, key.Name));
        else if (key.IsNotTrusted)
            yield return new(RuleCatalogue.Untrusted, Untrusted("foreign key", key.Table, key.Name));

        var actions = new[] { ("ON DELETE", key.DeleteAction), ("ON UPDATE", key.UpdateAction) }
            .Where(a => a.Item2 != "NO_ACTION")
            .Select(a => $"{a.Item1} {a.Item2.Replace('_', ' ')}")
            .ToList();
        if (actions.Count > 0)
            yield return new(RuleCatalogue.ForeignKeyAction, $"foreign key {name} {string.Join(" ", actions)} becomes NO ACTION");
    }

    private static Finding Trigger(TriggerInfo trigger) =>
        new(RuleCatalogue.Trigger,
            $"{(trigger.IsInsteadOf ? "INSTEAD OF" : "AFTER")} trigger {ObjectName.Quote(trigger.Name)} isn't carried over"
            + (trigger.IsInsteadOf ? ": memory-optimized tables only have AFTER triggers" : ""));
}
