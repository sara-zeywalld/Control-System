using System.Globalization;
using System.Text.Json;
using ControlSystem.BL.Catalog;
using ControlSystem.BL.Definitions;

namespace ControlSystem.BL.Querying;

public static class OperatorRules
{
    private static readonly ComparisonOperator[] StringOps =
    [
        ComparisonOperator.Eq, ComparisonOperator.Neq, ComparisonOperator.Contains, ComparisonOperator.NotContains,
        ComparisonOperator.StartsWith, ComparisonOperator.EndsWith, ComparisonOperator.In, ComparisonOperator.NotIn,
        ComparisonOperator.IsNull, ComparisonOperator.IsNotNull
    ];

    private static readonly ComparisonOperator[] OrderedOps =
    [
        ComparisonOperator.Eq, ComparisonOperator.Neq, ComparisonOperator.Gt, ComparisonOperator.Gte,
        ComparisonOperator.Lt, ComparisonOperator.Lte, ComparisonOperator.Between,
        ComparisonOperator.IsNull, ComparisonOperator.IsNotNull
    ];

    private static readonly ComparisonOperator[] NumberOps = [.. OrderedOps, ComparisonOperator.In, ComparisonOperator.NotIn];

    private static readonly ComparisonOperator[] BooleanOps =
        [ComparisonOperator.Eq, ComparisonOperator.Neq, ComparisonOperator.IsNull, ComparisonOperator.IsNotNull];

    public static IReadOnlyList<ComparisonOperator> AllowedFor(FieldType type) => type switch
    {
        FieldType.String => StringOps,
        FieldType.Number => NumberOps,
        FieldType.Date => OrderedOps,
        _ => BooleanOps
    };

    public static bool NeedsNoValue(ComparisonOperator op) => op is ComparisonOperator.IsNull or ComparisonOperator.IsNotNull;
    public static bool IsListOperator(ComparisonOperator op) => op is ComparisonOperator.In or ComparisonOperator.NotIn;
    public static bool SupportsFieldComparison(ComparisonOperator op) =>
        op is ComparisonOperator.Eq or ComparisonOperator.Neq or ComparisonOperator.Gt or ComparisonOperator.Gte
            or ComparisonOperator.Lt or ComparisonOperator.Lte;
}

/// <summary>המרת ערכי JSON שהגיעו מהלקוח / מה-AI לערך טיפוסי. מחזיר false עם הודעת שגיאה אם הערך לא מתאים.</summary>
public static class ValueCoercer
{
    public static bool TryCoerce(JsonElement element, FieldType type, out object? value, out string error)
    {
        value = null;
        error = "";
        switch (type)
        {
            case FieldType.Number:
                if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var d)) { value = d; return true; }
                if (element.ValueKind == JsonValueKind.String &&
                    decimal.TryParse(element.GetString()?.Replace(",", ""), NumberStyles.Number, CultureInfo.InvariantCulture, out d))
                { value = d; return true; }
                error = "הערך חייב להיות מספר";
                return false;

            case FieldType.Date:
                if (element.ValueKind == JsonValueKind.String &&
                    DateTime.TryParse(element.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var dt))
                { value = dt.Date; return true; }
                error = "הערך חייב להיות תאריך תקין (yyyy-MM-dd)";
                return false;

            case FieldType.Boolean:
                if (element.ValueKind is JsonValueKind.True or JsonValueKind.False) { value = element.GetBoolean(); return true; }
                if (element.ValueKind == JsonValueKind.String && bool.TryParse(element.GetString(), out var b)) { value = b; return true; }
                error = "הערך חייב להיות כן/לא";
                return false;

            default:
                if (element.ValueKind == JsonValueKind.String) { value = element.GetString(); return true; }
                if (element.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                { value = element.ToString(); return true; }
                error = "הערך חייב להיות טקסט";
                return false;
        }
    }
}
