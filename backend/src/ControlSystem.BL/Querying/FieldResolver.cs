using ControlSystem.BL.Catalog;
using ControlSystem.BL.Definitions;

namespace ControlSystem.BL.Querying;

public sealed record ResolvedField(FieldMeta Field, IReadOnlyList<RelationMeta> Hops, string Label);

public static class FieldResolver
{
    public static ResolvedField? Resolve(DataCatalog catalog, string rootEntity, FieldRef? reference, out string error)
    {
        error = "";
        if (reference is null || string.IsNullOrWhiteSpace(reference.Field))
        {
            error = "לא נבחר שדה";
            return null;
        }
        var resolved = catalog.ResolvePath(rootEntity, reference.Path ?? []);
        if (resolved is null)
        {
            error = $"הקשר '{string.Join(" › ", reference.Path ?? [])}' אינו קיים";
            return null;
        }
        var field = resolved.Value.Target.FindField(reference.Field);
        if (field is null)
        {
            error = $"השדה '{reference.Field}' אינו קיים בישות '{resolved.Value.Target.DisplayName}'";
            return null;
        }
        var label = string.Join(" › ", resolved.Value.Hops.Select(h => h.DisplayName).Append(field.DisplayName));
        return new ResolvedField(field, resolved.Value.Hops, label);
    }
}
