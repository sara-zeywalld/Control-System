using ControlSystem.BL.Catalog;
using ControlSystem.BL.Controls;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;

namespace ControlSystem.DAL.Persistence;

public sealed class DatabaseOptions
{
    /// <summary>הגדרות בקרות וקטלוג (קריאה/כתיבה).</summary>
    public string Metadata { get; set; } = "";
    /// <summary>הרצת בקרות על הנתונים העסקיים - מומלץ משתמש עם הרשאת קריאה בלבד.</summary>
    public string DataReadOnly { get; set; } = "";
}

/// <summary>טוען את הקטלוג העסקי מטבלאות ctl.*. הוספת ישות/שדה/קשר = הוספת שורות, בלי שינוי קוד.</summary>
public sealed class SqlCatalogProvider(DatabaseOptions db, IMemoryCache cache) : ICatalogProvider
{
    private sealed record EntityRow(string EntityKey, string DisplayName, string PluralName, string? Description, string? Synonyms,
        string SchemaName, string TableName, string PrimaryKeyColumn, bool IsQueryable);
    private sealed record FieldRow(string EntityKey, string FieldKey, string DisplayName, string? Description, string? Synonyms,
        string? ColumnName, string? SqlExpression, string DataType, bool IsRequired, bool ShowInResults, int SortOrder);
    private sealed record RelationRow(string FromEntityKey, string RelationKey, string ToEntityKey, string FromColumn, string ToColumn,
        string DisplayName, string? Description, string? Synonyms);

    public Task<DataCatalog> GetCatalogAsync(CancellationToken ct) =>
        cache.GetOrCreateAsync("catalog", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60);
            return await LoadAsync(ct);
        })!;

    private async Task<DataCatalog> LoadAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(db.Metadata);
        await conn.OpenAsync(ct);
        var entities = (await conn.QueryAsync<EntityRow>(new CommandDefinition(
            "SELECT * FROM ctl.Entity", cancellationToken: ct))).ToList();
        var fields = (await conn.QueryAsync<FieldRow>(new CommandDefinition(
            "SELECT * FROM ctl.Field ORDER BY SortOrder", cancellationToken: ct))).ToList();
        var relations = (await conn.QueryAsync<RelationRow>(new CommandDefinition(
            "SELECT * FROM ctl.Relation", cancellationToken: ct))).ToList();

        return new DataCatalog(entities.Select(e => new EntityMeta(
            e.EntityKey, e.DisplayName, e.PluralName, e.Description, Split(e.Synonyms),
            e.SchemaName, e.TableName, e.PrimaryKeyColumn, e.IsQueryable,
            fields.Where(f => f.EntityKey == e.EntityKey).Select(f => new FieldMeta(
                f.EntityKey, f.FieldKey, f.DisplayName, f.Description, Split(f.Synonyms), f.ColumnName, f.SqlExpression,
                Enum.Parse<FieldType>(f.DataType, ignoreCase: true), f.IsRequired, f.ShowInResults, f.SortOrder)).ToList(),
            relations.Where(r => r.FromEntityKey == e.EntityKey).Select(r => new RelationMeta(
                r.FromEntityKey, r.RelationKey, r.ToEntityKey, r.FromColumn, r.ToColumn,
                r.DisplayName, r.Description, Split(r.Synonyms))).ToList())));
    }

    private static IReadOnlyList<string> Split(string? s) =>
        string.IsNullOrWhiteSpace(s) ? [] : s.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
}
