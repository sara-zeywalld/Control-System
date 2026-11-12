using System.Text.Json.Serialization;

namespace ControlSystem.Core.Catalog;

[JsonConverter(typeof(JsonStringEnumConverter<FieldType>))]
public enum FieldType { String, Number, Date, Boolean }

public sealed record FieldMeta(
    string EntityKey, string Key, string DisplayName, string? Description, IReadOnlyList<string> Synonyms,
    string? ColumnName, string? SqlExpression, FieldType Type, bool IsRequired, bool ShowInResults, int SortOrder);

/// <summary>קשר רבים-לאחד מישות מקור לישות יעד.</summary>
public sealed record RelationMeta(
    string FromEntityKey, string Key, string ToEntityKey, string FromColumn, string ToColumn,
    string DisplayName, string? Description, IReadOnlyList<string> Synonyms);

public sealed record EntityMeta(
    string Key, string DisplayName, string PluralName, string? Description, IReadOnlyList<string> Synonyms,
    string Schema, string Table, string PrimaryKeyColumn, bool IsQueryable,
    IReadOnlyList<FieldMeta> Fields, IReadOnlyList<RelationMeta> Relations)
{
    public FieldMeta? FindField(string key) => Fields.FirstOrDefault(f => f.Key == key);
    public RelationMeta? FindRelation(string key) => Relations.FirstOrDefault(r => r.Key == key);
}

public sealed class DataCatalog
{
    private readonly Dictionary<string, EntityMeta> _entities;

    public DataCatalog(IEnumerable<EntityMeta> entities) =>
        _entities = entities.ToDictionary(e => e.Key, StringComparer.Ordinal);

    public IReadOnlyCollection<EntityMeta> Entities => _entities.Values;

    public EntityMeta? FindEntity(string key) => _entities.GetValueOrDefault(key);

    public EntityMeta GetEntity(string key) =>
        FindEntity(key) ?? throw new KeyNotFoundException($"ישות לא קיימת: {key}");

    /// <summary>מפענח נתיב קשרים. מחזיר null אם אחד הקשרים לא קיים.</summary>
    public (List<RelationMeta> Hops, EntityMeta Target)? ResolvePath(string rootKey, IEnumerable<string> path)
    {
        var current = FindEntity(rootKey);
        if (current is null) return null;
        var hops = new List<RelationMeta>();
        foreach (var key in path)
        {
            var rel = current.FindRelation(key);
            if (rel is null) return null;
            var next = FindEntity(rel.ToEntityKey);
            if (next is null) return null;
            hops.Add(rel);
            current = next;
        }
        return (hops, current);
    }
}
