using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControlSystem.BL.Definitions;

public enum LogicalOperator { And, Or }

public enum ComparisonOperator
{
    Eq, Neq, Gt, Gte, Lt, Lte, Between,
    In, NotIn,
    Contains, NotContains, StartsWith, EndsWith,
    IsNull, IsNotNull
}

/// <summary>הפניה לשדה עסקי: נתיב קשרים מהישות הראשית (ריק = הישות הראשית) ומפתח השדה.</summary>
public sealed record FieldRef(List<string> Path, string Field)
{
    public string PathKey => string.Join('/', Path);
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(GroupNode), "group")]
[JsonDerivedType(typeof(ConditionNode), "condition")]
public abstract record DefinitionNode;

/// <summary>קבוצת תנאים עם AND/OR; ניתן לקנן קבוצות.</summary>
public sealed record GroupNode(LogicalOperator Operator, List<DefinitionNode> Items) : DefinitionNode;

/// <summary>
/// תנאי בודד. ערך להשוואה הוא אחד מ: Value (ערך בודד), Values (רשימה / טווח), CompareField (שדה אחר).
/// </summary>
public sealed record ConditionNode(
    FieldRef Field,
    ComparisonOperator Operator,
    JsonElement? Value = null,
    List<JsonElement>? Values = null,
    FieldRef? CompareField = null) : DefinitionNode;

/// <summary>הגדרת בקרה: ישות ראשית + עץ תנאים. הרשומות שמקיימות את התנאי הן החריגות.</summary>
public sealed record ControlDefinition(string RootEntity, GroupNode Root);

public static class ControlJson
{
    public static JsonSerializerOptions Options { get; } = Create();

    public static void Configure(JsonSerializerOptions o)
    {
        o.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        o.PropertyNameCaseInsensitive = true;
        o.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        o.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
    }

    private static JsonSerializerOptions Create()
    {
        var o = new JsonSerializerOptions();
        Configure(o);
        return o;
    }

    public static ControlDefinition Parse(string json) =>
        JsonSerializer.Deserialize<ControlDefinition>(json, Options)
        ?? throw new JsonException("Empty control definition");

    public static string Serialize(ControlDefinition d) => JsonSerializer.Serialize(d, Options);
}
