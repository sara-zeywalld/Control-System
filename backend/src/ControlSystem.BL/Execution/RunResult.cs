using ControlSystem.BL.Catalog;
using ControlSystem.BL.Querying;

namespace ControlSystem.BL.Execution;

public sealed record RunColumn(string Key, string Label, FieldType Type);

public sealed record RunRow(IReadOnlyDictionary<string, object?> Values, IReadOnlyList<string> Reasons);

public sealed record RunResult(
    int? ControlId,
    string ControlName,
    IReadOnlyList<RunColumn> Columns,
    IReadOnlyList<RunRow> Rows,
    int TotalCount,
    bool Truncated,
    long DurationMs,
    string GeneratedSql,
    DateTime RanAtUtc);

/// <summary>שורות גולמיות כפי שחזרו מהמסד, לפי שמות העמודות (aliases) שנוצרו ב-QueryBuilder.</summary>
public sealed record RawResult(IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows);

public interface IQueryExecutor
{
    Task<RawResult> ExecuteAsync(BuiltQuery query, CancellationToken ct);
}

public static class ResultMapper
{
    public static (List<RunRow> Rows, int Total) Map(BuiltQuery query, RawResult raw)
    {
        var rows = new List<RunRow>(raw.Rows.Count);
        var total = 0;
        foreach (var r in raw.Rows)
        {
            if (total == 0 && r.TryGetValue(BuiltQuery.TotalAlias, out var t) && t is not null and not DBNull)
                total = Convert.ToInt32(t);

            var values = query.Columns.ToDictionary(c => c.Key, c => Normalize(r[c.Alias]));
            var reasons = new List<string>();
            foreach (var leaf in query.Leaves)
            {
                if (Convert.ToInt32(r[leaf.FlagAlias]) != 1) continue;
                reasons.Add(Reason(leaf, r));
            }
            rows.Add(new RunRow(values, reasons));
        }
        return (rows, total);
    }

    private static string Reason(LeafInfo leaf, IReadOnlyDictionary<string, object?> row)
    {
        var type = leaf.Left.Field.Type;
        var op = leaf.Condition.Operator;
        if (OperatorRules.NeedsNoValue(op))
            return $"{leaf.Left.Label} {ConditionText.Operator(op, type)}";

        var actual = ConditionText.Format(Normalize(row[leaf.ValueAlias]));
        var rhs = ConditionText.Right(leaf.Condition, leaf.Right, leaf.Expected);
        if (leaf.Right is not null)
            rhs += $" ({ConditionText.Format(Normalize(row[leaf.CompareAlias]))})";
        return $"{leaf.Left.Label} ({actual}) {ConditionText.Operator(op, type)}{rhs}";
    }

    private static object? Normalize(object? v) => v is DBNull ? null : v;
}
