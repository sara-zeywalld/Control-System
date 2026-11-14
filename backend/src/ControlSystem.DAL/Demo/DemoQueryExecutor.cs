using ControlSystem.BL.Catalog;
using ControlSystem.BL.Definitions;
using ControlSystem.BL.Execution;
using ControlSystem.BL.Querying;

namespace ControlSystem.DAL.Demo;

/// <summary>
/// מריץ בקרה על נתוני דוגמה בזיכרון במקום SQL Server. משתמש באותו BuiltQuery (אותם תנאים, קשרים וסדר עלים),
/// ולכן ה-UI, האימות והסברי הכשל זהים למצב האמיתי.
/// </summary>
public sealed class DemoQueryExecutor : IQueryExecutor
{
    private readonly Dictionary<string, List<Dictionary<string, object?>>> _tables = DemoData.CreateTables();

    public Task<RawResult> ExecuteAsync(BuiltQuery query, CancellationToken ct)
    {
        var def = query.Definition ?? throw new InvalidOperationException("Demo executor needs the control definition");
        var root = DemoData.Catalog.GetEntity(def.RootEntity);
        var matches = new List<Dictionary<string, object?>>();

        foreach (var row in _tables[root.Key].OrderBy(r => (decimal)r[root.PrimaryKeyColumn]!))
        {
            var flags = query.Leaves.Select(l => EvalLeaf(l, row)).ToArray();
            var cursor = 0;
            if (!EvalNode(def.Root, flags, ref cursor)) continue;
            matches.Add(ToOutput(query, row, flags));
        }

        var total = matches.Count;
        var page = matches.Take(query.MaxRows).Select(m => { m[BuiltQuery.TotalAlias] = total; return (IReadOnlyDictionary<string, object?>)m; }).ToList();
        return Task.FromResult(new RawResult(page));
    }

    private static bool EvalNode(DefinitionNode node, bool[] flags, ref int cursor)
    {
        if (node is ConditionNode) return flags[cursor++];
        var g = (GroupNode)node;
        var results = new List<bool>();
        foreach (var item in g.Items) results.Add(EvalNode(item, flags, ref cursor));
        return g.Operator == LogicalOperator.And ? results.All(r => r) : results.Any(r => r);
    }

    private Dictionary<string, object?> ToOutput(BuiltQuery query, Dictionary<string, object?> row, bool[] flags)
    {
        var o = new Dictionary<string, object?>();
        var rootEntity = DemoData.Catalog.GetEntity(query.Definition!.RootEntity);
        foreach (var c in query.Columns)
            o[c.Alias] = Value(row, [], rootEntity.FindField(c.Key)!);
        foreach (var leaf in query.Leaves)
        {
            o[leaf.FlagAlias] = flags[leaf.Index] ? 1 : 0;
            o[leaf.ValueAlias] = Value(row, leaf.Left.Hops, leaf.Left.Field);
            if (leaf.Right is not null) o[leaf.CompareAlias] = Value(row, leaf.Right.Hops, leaf.Right.Field);
        }
        return o;
    }

    private object? Value(Dictionary<string, object?> row, IReadOnlyList<RelationMeta> hops, FieldMeta field)
    {
        Dictionary<string, object?>? current = row;
        foreach (var hop in hops)
        {
            var key = current?.GetValueOrDefault(hop.FromColumn);
            current = key is null ? null : _tables[hop.ToEntityKey].FirstOrDefault(r => Equals(r[hop.ToColumn], key));
        }
        return current?.GetValueOrDefault(field.ColumnName ?? field.Key);
    }

    private bool EvalLeaf(LeafInfo leaf, Dictionary<string, object?> row)
    {
        var left = Value(row, leaf.Left.Hops, leaf.Left.Field);
        var op = leaf.Condition.Operator;
        var type = leaf.Left.Field.Type;

        if (op is ComparisonOperator.IsNull or ComparisonOperator.IsNotNull)
        {
            var empty = left is null || (type == FieldType.String && string.IsNullOrWhiteSpace((string)left));
            return op == ComparisonOperator.IsNull ? empty : !empty;
        }
        if (left is null) return false;

        if (leaf.Right is not null)
        {
            var right = Value(row, leaf.Right.Hops, leaf.Right.Field);
            return right is not null && Compare(op, Cmp(left, right));
        }

        var e = leaf.Expected;
        switch (op)
        {
            case ComparisonOperator.Between: return Cmp(left, e[0]) >= 0 && Cmp(left, e[1]) <= 0;
            case ComparisonOperator.In: return e.Any(v => Cmp(left, v) == 0);
            case ComparisonOperator.NotIn: return e.All(v => Cmp(left, v) != 0);
            case ComparisonOperator.Contains: return ((string)left).Contains((string)e[0], StringComparison.OrdinalIgnoreCase);
            case ComparisonOperator.NotContains: return !((string)left).Contains((string)e[0], StringComparison.OrdinalIgnoreCase);
            case ComparisonOperator.StartsWith: return ((string)left).StartsWith((string)e[0], StringComparison.OrdinalIgnoreCase);
            case ComparisonOperator.EndsWith: return ((string)left).EndsWith((string)e[0], StringComparison.OrdinalIgnoreCase);
            default: return Compare(op, Cmp(left, e[0]));
        }
    }

    private static bool Compare(ComparisonOperator op, int c) => op switch
    {
        ComparisonOperator.Eq => c == 0,
        ComparisonOperator.Neq => c != 0,
        ComparisonOperator.Gt => c > 0,
        ComparisonOperator.Gte => c >= 0,
        ComparisonOperator.Lt => c < 0,
        ComparisonOperator.Lte => c <= 0,
        _ => false
    };

    private static int Cmp(object a, object b) => a switch
    {
        string s => string.Compare(s, (string)b, StringComparison.OrdinalIgnoreCase),
        bool x => x.CompareTo((bool)b),
        DateTime d => d.Date.CompareTo(((DateTime)b).Date),
        _ => Convert.ToDecimal(a).CompareTo(Convert.ToDecimal(b))
    };
}
