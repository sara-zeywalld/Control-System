using ControlSystem.BL.Catalog;
using ControlSystem.BL.Definitions;

namespace ControlSystem.BL.Querying;

/// <summary>מתרגם הגדרת בקרה למשפט עסקי קריא, להצגה ברשימה ובמסך אישור ה-AI.</summary>
public static class ControlDescriber
{
    public static string Describe(ControlDefinition definition, DataCatalog catalog)
    {
        var root = catalog.FindEntity(definition.RootEntity);
        var body = Node(definition.Root, catalog, definition.RootEntity, top: true);
        return $"{root?.PluralName ?? definition.RootEntity} שבהם: {body}";
    }

    private static string Node(DefinitionNode node, DataCatalog catalog, string rootKey, bool top)
    {
        switch (node)
        {
            case GroupNode g:
                var parts = g.Items.Select(i => Node(i, catalog, rootKey, false)).ToList();
                var text = string.Join(g.Operator == LogicalOperator.And ? " וגם " : " או ", parts);
                return top || parts.Count == 1 ? text : $"({text})";
            case ConditionNode c:
                var left = FieldResolver.Resolve(catalog, rootKey, c.Field, out var err);
                if (left is null) return err;
                var right = c.CompareField is null ? null : FieldResolver.Resolve(catalog, rootKey, c.CompareField, out _);
                var expected = new List<object>();
                foreach (var e in c.Values ?? [])
                    if (ValueCoercer.TryCoerce(e, left.Field.Type, out var v, out _)) expected.Add(v!);
                if (c.Value is { } single && ValueCoercer.TryCoerce(single, left.Field.Type, out var sv, out _)) expected.Add(sv!);
                return ConditionText.Describe(new LeafInfo(0, c, left, right, expected));
            default:
                return "";
        }
    }
}
