using System.Globalization;
using ControlSystem.BL.Catalog;
using ControlSystem.BL.Definitions;

namespace ControlSystem.BL.Querying;

/// <summary>ניסוח עסקי (עברית) של תנאים וערכים - משמש לתיאור בקרה ולהסבר מדוע רשומה נכשלה.</summary>
public static class ConditionText
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("he-IL");

    public static string Operator(ComparisonOperator op, FieldType type)
    {
        var date = type == FieldType.Date;
        return op switch
        {
            ComparisonOperator.Eq => "שווה ל-",
            ComparisonOperator.Neq => "שונה מ-",
            ComparisonOperator.Gt => date ? "מאוחר מ-" : "גדול מ-",
            ComparisonOperator.Gte => date ? "מאוחר או שווה ל-" : "גדול או שווה ל-",
            ComparisonOperator.Lt => date ? "מוקדם מ-" : "קטן מ-",
            ComparisonOperator.Lte => date ? "מוקדם או שווה ל-" : "קטן או שווה ל-",
            ComparisonOperator.Between => "בטווח ",
            ComparisonOperator.In => "אחד מ: ",
            ComparisonOperator.NotIn => "אינו אחד מ: ",
            ComparisonOperator.Contains => "מכיל ",
            ComparisonOperator.NotContains => "אינו מכיל ",
            ComparisonOperator.StartsWith => "מתחיל ב-",
            ComparisonOperator.EndsWith => "מסתיים ב-",
            ComparisonOperator.IsNull => "חסר",
            ComparisonOperator.IsNotNull => "מולא",
            _ => op.ToString()
        };
    }

    public static string Format(object? value)
    {
        if (value is null or DBNull) return "ריק";
        return value switch
        {
            bool b => b ? "כן" : "לא",
            DateTime d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            decimal m => m.ToString("#,0.##", Culture),
            IFormattable f => f.ToString(null, Culture),
            _ => value.ToString() ?? ""
        };
    }

    /// <summary>הצד הימני של התנאי: ערך/ים או שם שדה אחר.</summary>
    public static string Right(ConditionNode c, ResolvedField? right, IReadOnlyList<object> expected)
    {
        if (OperatorRules.NeedsNoValue(c.Operator)) return "";
        if (right is not null) return $"\"{right.Label}\"";
        return c.Operator switch
        {
            ComparisonOperator.Between when expected.Count == 2 => $"{Format(expected[0])} עד {Format(expected[1])}",
            ComparisonOperator.In or ComparisonOperator.NotIn => string.Join(", ", expected.Select(Format)),
            _ => expected.Count > 0 ? Format(expected[0]) : ""
        };
    }

    /// <summary>תנאי כמשפט, למשל: גיל קטן מ-18.</summary>
    public static string Describe(LeafInfo leaf) =>
        $"{leaf.Left.Label} {Operator(leaf.Condition.Operator, leaf.Left.Field.Type)}{Right(leaf.Condition, leaf.Right, leaf.Expected)}".Trim();
}
