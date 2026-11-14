using ControlSystem.BL.Catalog;
using ControlSystem.BL.Definitions;

namespace ControlSystem.BL.Querying;

public static class Limits
{
    public const int MaxDepth = 5;
    public const int MaxConditions = 50;
    public const int MaxListValues = 100;
    public const int MaxPathLength = 3;
    public const int MaxTextLength = 200;
}

/// <summary>בדיקת תקינות של הגדרת בקרה מול הקטלוג. משמש גם את ה-UI, גם את מנגנון ה-AI וגם לפני הרצה.</summary>
public static class ControlValidator
{
    public static List<string> Validate(ControlDefinition? definition, DataCatalog catalog)
    {
        var errors = new List<string>();
        if (definition is null) { errors.Add("הגדרת הבקרה חסרה"); return errors; }

        var root = catalog.FindEntity(definition.RootEntity ?? "");
        if (root is null) { errors.Add($"הנתונים העסקיים '{definition.RootEntity}' אינם קיימים"); return errors; }
        if (!root.IsQueryable) { errors.Add($"לא ניתן להגדיר בקרה על '{root.DisplayName}'"); return errors; }
        if (definition.Root is null || definition.Root.Items is null || definition.Root.Items.Count == 0)
        {
            errors.Add("יש להגדיר לפחות תנאי אחד");
            return errors;
        }

        var conditions = 0;
        Walk(definition.Root, 1);
        if (conditions > Limits.MaxConditions) errors.Add($"מספר התנאים חורג מהמותר ({Limits.MaxConditions})");
        return errors;

        void Walk(DefinitionNode node, int depth)
        {
            if (depth > Limits.MaxDepth) { errors.Add($"עומק הקינון חורג מהמותר ({Limits.MaxDepth})"); return; }
            switch (node)
            {
                case GroupNode g:
                    if (g.Items is null || g.Items.Count == 0) { errors.Add("קבוצת תנאים ריקה"); return; }
                    foreach (var child in g.Items) Walk(child, depth + 1);
                    break;
                case ConditionNode c:
                    conditions++;
                    ValidateCondition(c);
                    break;
                default:
                    errors.Add("סוג צומת לא מוכר");
                    break;
            }
        }

        void ValidateCondition(ConditionNode c)
        {
            if (c.Field?.Path?.Count > Limits.MaxPathLength) { errors.Add("נתיב הקשרים ארוך מדי"); return; }
            var left = FieldResolver.Resolve(catalog, definition.RootEntity!, c.Field, out var err);
            if (left is null) { errors.Add(err); return; }
            var name = left.Label;
            var type = left.Field.Type;

            if (!OperatorRules.AllowedFor(type).Contains(c.Operator))
            {
                errors.Add($"'{name}': הפעולה '{c.Operator}' אינה נתמכת בשדה מסוג זה");
                return;
            }
            if (OperatorRules.NeedsNoValue(c.Operator)) return;

            if (c.CompareField is not null)
            {
                if (!OperatorRules.SupportsFieldComparison(c.Operator))
                {
                    errors.Add($"'{name}': לא ניתן להשוות לשדה אחר בפעולה זו");
                    return;
                }
                var right = FieldResolver.Resolve(catalog, definition.RootEntity!, c.CompareField, out err);
                if (right is null) { errors.Add(err); return; }
                if (right.Field.Type != type) errors.Add($"'{name}': לא ניתן להשוות לשדה '{right.Label}' מסוג שונה");
                return;
            }

            var values = c.Values ?? [];
            if (OperatorRules.IsListOperator(c.Operator) || c.Operator == ComparisonOperator.Between)
            {
                var expected = c.Operator == ComparisonOperator.Between ? 2 : -1;
                if (expected == 2 && values.Count != 2) { errors.Add($"'{name}': יש להזין שני ערכים לטווח"); return; }
                if (expected == -1 && (values.Count == 0 || values.Count > Limits.MaxListValues))
                {
                    errors.Add($"'{name}': יש להזין בין 1 ל-{Limits.MaxListValues} ערכים");
                    return;
                }
                foreach (var v in values) CheckValue(name, v, type);
                return;
            }

            if (c.Value is null || c.Value.Value.ValueKind is System.Text.Json.JsonValueKind.Null or System.Text.Json.JsonValueKind.Undefined)
            {
                errors.Add($"'{name}': חסר ערך להשוואה");
                return;
            }
            CheckValue(name, c.Value.Value, type);
        }

        void CheckValue(string name, System.Text.Json.JsonElement v, FieldType type)
        {
            if (!ValueCoercer.TryCoerce(v, type, out var coerced, out var e))
                errors.Add($"'{name}': {e}");
            else if (coerced is string s && s.Length > Limits.MaxTextLength)
                errors.Add($"'{name}': הטקסט ארוך מדי");
        }
    }

    public static void EnsureValid(ControlDefinition? definition, DataCatalog catalog)
    {
        var errors = Validate(definition, catalog);
        if (errors.Count > 0) throw new ControlValidationException(errors);
    }
}
