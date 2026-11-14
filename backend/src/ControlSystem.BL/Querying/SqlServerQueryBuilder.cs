using System.Text;
using System.Text.RegularExpressions;
using ControlSystem.BL.Catalog;
using ControlSystem.BL.Definitions;

namespace ControlSystem.BL.Querying;

public sealed record QueryParameter(string Name, object Value, FieldType Type);

/// <summary>עמודה שתוצג בתוצאות. Alias הוא שם העמודה בשאילתה (נוצר ע"י המערכת, לא מהקלט).</summary>
public sealed record ResultColumn(string Alias, string Key, string Label, FieldType Type);

/// <summary>תנאי בודד בעץ, עם הנתונים הדרושים להסבר מדוע רשומה נכשלה בו.</summary>
public sealed record LeafInfo(
    int Index, ConditionNode Condition, ResolvedField Left, ResolvedField? Right, IReadOnlyList<object> Expected)
{
    public string FlagAlias => $"f{Index}";
    public string ValueAlias => $"v{Index}";
    public string CompareAlias => $"w{Index}";
}

public sealed record QueryOptions(int MaxRows = 1000);

public sealed record BuiltQuery(
    string Sql,
    IReadOnlyList<QueryParameter> Parameters,
    IReadOnlyList<ResultColumn> Columns,
    IReadOnlyList<LeafInfo> Leaves,
    int MaxRows,
    ControlDefinition? Definition = null)
{
    public const string TotalAlias = "total";
}

public interface IQueryBuilder
{
    BuiltQuery Build(ControlDefinition definition, DataCatalog catalog, QueryOptions? options = null);
}

/// <summary>
/// מתרגם הגדרת בקרה ל-T-SQL. אין שימוש בטקסט חופשי: מזהים (טבלאות/עמודות) נלקחים רק מהקטלוג ומצוטטים,
/// וכל ערך מהמשתמש עובר כפרמטר.
/// </summary>
public sealed partial class SqlServerQueryBuilder : IQueryBuilder
{
    [GeneratedRegex(@"^[\p{L}_][\p{L}\p{N}_ ]*$")]
    private static partial Regex SafeIdentifier();

    public BuiltQuery Build(ControlDefinition definition, DataCatalog catalog, QueryOptions? options = null)
    {
        options ??= new QueryOptions();
        ControlValidator.EnsureValid(definition, catalog);
        var root = catalog.GetEntity(definition.RootEntity);
        var ctx = new BuildContext(catalog, root);

        var where = BuildNode(definition.Root, ctx);

        var sql = new StringBuilder();
        sql.Append("SELECT TOP (").Append(Math.Clamp(options.MaxRows, 1, 10_000)).Append(") ");

        var columns = new List<ResultColumn>();
        var select = new List<string>();
        var i = 0;
        foreach (var f in root.Fields.Where(f => f.ShowInResults).OrderBy(f => f.SortOrder))
        {
            var alias = $"c{i++}";
            columns.Add(new ResultColumn(alias, f.Key, f.DisplayName, f.Type));
            select.Add($"{FieldExpression(f, "t0")} AS {Quote(alias)}");
        }
        foreach (var leaf in ctx.Leaves)
        {
            select.Add($"CASE WHEN {ctx.Predicates[leaf.Index]} THEN 1 ELSE 0 END AS {Quote(leaf.FlagAlias)}");
            select.Add($"{ctx.LeftExpr[leaf.Index]} AS {Quote(leaf.ValueAlias)}");
            if (leaf.Right is not null) select.Add($"{ctx.RightExpr[leaf.Index]} AS {Quote(leaf.CompareAlias)}");
        }
        select.Add($"COUNT(*) OVER() AS {Quote(BuiltQuery.TotalAlias)}");

        sql.AppendLine(string.Join(", ", select));
        sql.AppendLine($"FROM {Quote(root.Schema)}.{Quote(root.Table)} AS {Quote("t0")}");
        foreach (var join in ctx.Joins) sql.AppendLine(join);
        sql.AppendLine($"WHERE {where}");
        sql.Append($"ORDER BY {Quote("t0")}.{Quote(root.PrimaryKeyColumn)}");

        return new BuiltQuery(sql.ToString(), ctx.Parameters, columns, ctx.Leaves, options.MaxRows, definition);
    }

    private string BuildNode(DefinitionNode node, BuildContext ctx)
    {
        switch (node)
        {
            case GroupNode g:
                var parts = g.Items.Select(n => BuildNode(n, ctx)).ToList();
                var op = g.Operator == LogicalOperator.And ? " AND " : " OR ";
                return parts.Count == 1 ? parts[0] : "(" + string.Join(op, parts) + ")";
            case ConditionNode c:
                return BuildCondition(c, ctx);
            default:
                throw new UnsafeQueryException("סוג צומת לא נתמך");
        }
    }

    private string BuildCondition(ConditionNode c, BuildContext ctx)
    {
        var left = FieldResolver.Resolve(ctx.Catalog, ctx.Root.Key, c.Field, out _)!;
        var leftExpr = ctx.Expression(left);
        string predicate;
        ResolvedField? right = null;
        string? rightExpr = null;
        var expected = new List<object>();

        if (OperatorRules.NeedsNoValue(c.Operator))
        {
            var isNull = c.Operator == ComparisonOperator.IsNull;
            if (left.Field.Type == FieldType.String)
                predicate = isNull
                    ? $"({leftExpr} IS NULL OR LTRIM(RTRIM({leftExpr})) = N'')"
                    : $"({leftExpr} IS NOT NULL AND LTRIM(RTRIM({leftExpr})) <> N'')";
            else
                predicate = $"{leftExpr} IS {(isNull ? "NULL" : "NOT NULL")}";
        }
        else if (c.CompareField is not null)
        {
            right = FieldResolver.Resolve(ctx.Catalog, ctx.Root.Key, c.CompareField, out _)!;
            rightExpr = ctx.Expression(right);
            predicate = $"{leftExpr} {SqlOperator(c.Operator)} {rightExpr}";
        }
        else
        {
            var type = left.Field.Type;
            object Coerce(System.Text.Json.JsonElement e)
            {
                ValueCoercer.TryCoerce(e, type, out var v, out _);
                expected.Add(v!);
                return v!;
            }

            switch (c.Operator)
            {
                case ComparisonOperator.Between:
                    predicate = $"{leftExpr} BETWEEN {ctx.AddParam(Coerce(c.Values![0]), type)} AND {ctx.AddParam(Coerce(c.Values![1]), type)}";
                    break;
                case ComparisonOperator.In:
                case ComparisonOperator.NotIn:
                    var names = c.Values!.Select(v => ctx.AddParam(Coerce(v), type));
                    predicate = $"{leftExpr} {(c.Operator == ComparisonOperator.In ? "IN" : "NOT IN")} ({string.Join(", ", names)})";
                    break;
                case ComparisonOperator.Contains:
                case ComparisonOperator.NotContains:
                case ComparisonOperator.StartsWith:
                case ComparisonOperator.EndsWith:
                    var text = (string)Coerce(c.Value!.Value);
                    var escaped = EscapeLike(text);
                    var pattern = c.Operator switch
                    {
                        ComparisonOperator.StartsWith => escaped + "%",
                        ComparisonOperator.EndsWith => "%" + escaped,
                        _ => "%" + escaped + "%"
                    };
                    var not = c.Operator == ComparisonOperator.NotContains ? "NOT " : "";
                    predicate = $"{leftExpr} {not}LIKE {ctx.AddParam(pattern, FieldType.String)} ESCAPE N'\\'";
                    break;
                default:
                    predicate = $"{leftExpr} {SqlOperator(c.Operator)} {ctx.AddParam(Coerce(c.Value!.Value), type)}";
                    break;
            }
        }

        var leaf = new LeafInfo(ctx.Leaves.Count, c, left, right, expected);
        ctx.Leaves.Add(leaf);
        ctx.Predicates.Add(predicate);
        ctx.LeftExpr.Add(leftExpr);
        ctx.RightExpr.Add(rightExpr);
        return predicate;
    }

    private static string SqlOperator(ComparisonOperator op) => op switch
    {
        ComparisonOperator.Eq => "=",
        ComparisonOperator.Neq => "<>",
        ComparisonOperator.Gt => ">",
        ComparisonOperator.Gte => ">=",
        ComparisonOperator.Lt => "<",
        ComparisonOperator.Lte => "<=",
        _ => throw new UnsafeQueryException($"פעולה לא נתמכת: {op}")
    };

    public static string EscapeLike(string s) =>
        s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_").Replace("[", "\\[");

    internal static string Quote(string identifier)
    {
        if (!SafeIdentifier().IsMatch(identifier))
            throw new UnsafeQueryException($"מזהה לא חוקי בקטלוג: {identifier}");
        return "[" + identifier + "]";
    }

    internal static string FieldExpression(FieldMeta field, string alias) =>
        field.SqlExpression is not null
            ? field.SqlExpression.Replace("{t}", Quote(alias))
            : $"{Quote(alias)}.{Quote(field.ColumnName!)}";

    private sealed class BuildContext(DataCatalog catalog, EntityMeta root)
    {
        private readonly Dictionary<string, string> _aliases = new() { [""] = "t0" };
        public DataCatalog Catalog { get; } = catalog;
        public EntityMeta Root { get; } = root;
        public List<string> Joins { get; } = [];
        public List<QueryParameter> Parameters { get; } = [];
        public List<LeafInfo> Leaves { get; } = [];
        public List<string> Predicates { get; } = [];
        public List<string> LeftExpr { get; } = [];
        public List<string?> RightExpr { get; } = [];

        public string AddParam(object value, FieldType type)
        {
            var name = $"@p{Parameters.Count}";
            Parameters.Add(new QueryParameter(name, value, type));
            return name;
        }

        /// <summary>מחזיר ביטוי SQL לשדה, ומוסיף LEFT JOIN לכל קשר בנתיב (פעם אחת לכל נתיב).</summary>
        public string Expression(ResolvedField resolved)
        {
            var key = "";
            var parentAlias = "t0";
            foreach (var hop in resolved.Hops)
            {
                key = key.Length == 0 ? hop.Key : key + "/" + hop.Key;
                if (!_aliases.TryGetValue(key, out var alias))
                {
                    alias = $"t{_aliases.Count}";
                    _aliases[key] = alias;
                    var target = Catalog.GetEntity(hop.ToEntityKey);
                    Joins.Add($"LEFT JOIN {Quote(target.Schema)}.{Quote(target.Table)} AS {Quote(alias)} " +
                              $"ON {Quote(alias)}.{Quote(hop.ToColumn)} = {Quote(parentAlias)}.{Quote(hop.FromColumn)}");
                }
                parentAlias = alias;
            }
            return FieldExpression(resolved.Field, parentAlias);
        }
    }
}
