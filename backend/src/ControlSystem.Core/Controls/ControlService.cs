using System.Diagnostics;
using ControlSystem.Core.Catalog;
using ControlSystem.Core.Definitions;
using ControlSystem.Core.Execution;
using ControlSystem.Core.Querying;

namespace ControlSystem.Core.Controls;

public sealed record ControlView(
    int Id, string Name, string? Description, ControlDefinition Definition, string RootEntityName,
    string Summary, string Source, DateTime CreatedAt, DateTime UpdatedAt, LastRunInfo? LastRun);

/// <summary>ניהול הגדרות בקרה: יצירה, עריכה, שכפול (שימוש חוזר) ומחיקה. תמיד עם ולידציה מול הקטלוג.</summary>
public sealed class ControlService(IControlRepository repo, ICatalogProvider catalogs)
{
    public async Task<IReadOnlyList<ControlView>> ListAsync(CancellationToken ct)
    {
        var catalog = await catalogs.GetCatalogAsync(ct);
        return (await repo.ListAsync(ct)).Select(r => ToView(r, catalog)).ToList();
    }

    public async Task<ControlView> GetAsync(int id, CancellationToken ct)
    {
        var catalog = await catalogs.GetCatalogAsync(ct);
        var record = await repo.GetAsync(id, ct) ?? throw new NotFoundException("הבקרה לא נמצאה");
        return ToView(record, catalog);
    }

    public async Task<ControlView> CreateAsync(ControlInput input, CancellationToken ct)
    {
        await ValidateAsync(input, ct);
        var id = await repo.CreateAsync(Normalize(input), ct);
        return await GetAsync(id, ct);
    }

    public async Task<ControlView> UpdateAsync(int id, ControlInput input, CancellationToken ct)
    {
        await ValidateAsync(input, ct);
        if (!await repo.UpdateAsync(id, Normalize(input), ct)) throw new NotFoundException("הבקרה לא נמצאה");
        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct)
    {
        if (!await repo.DeleteAsync(id, ct)) throw new NotFoundException("הבקרה לא נמצאה");
    }

    public async Task<ControlView> DuplicateAsync(int id, CancellationToken ct)
    {
        var src = await repo.GetAsync(id, ct) ?? throw new NotFoundException("הבקרה לא נמצאה");
        var newId = await repo.CreateAsync(
            new ControlInput($"{src.Name} (עותק)", src.Description, src.Definition, src.Source), ct);
        return await GetAsync(newId, ct);
    }

    private async Task ValidateAsync(ControlInput input, CancellationToken ct)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(input.Name)) errors.Add("יש להזין שם לבקרה");
        else if (input.Name.Length > 200) errors.Add("שם הבקרה ארוך מדי");
        if (input.Description?.Length > 1000) errors.Add("התיאור ארוך מדי");
        errors.AddRange(ControlValidator.Validate(input.Definition, await catalogs.GetCatalogAsync(ct)));
        if (errors.Count > 0) throw new ControlValidationException(errors);
    }

    private static ControlInput Normalize(ControlInput i) =>
        i with { Name = i.Name.Trim(), Description = i.Description?.Trim(), Source = i.Source == "ai" ? "ai" : "manual" };

    private static ControlView ToView(ControlRecord r, DataCatalog catalog) => new(
        r.Id, r.Name, r.Description, r.Definition,
        catalog.FindEntity(r.Definition.RootEntity)?.PluralName ?? r.Definition.RootEntity,
        ControlDescriber.Describe(r.Definition, catalog), r.Source, r.CreatedAt, r.UpdatedAt, r.LastRun);
}

/// <summary>הרצת בקרה: ולידציה → בניית שאילתה → בדיקת בטיחות → הרצה → הסבר סיבות כשל.</summary>
public sealed class ControlRunner(
    ICatalogProvider catalogs, IControlRepository repo, IQueryBuilder builder, IQueryExecutor executor)
{
    public const int MaxRows = 1000;

    public async Task<RunResult> RunSavedAsync(int id, CancellationToken ct)
    {
        var record = await repo.GetAsync(id, ct) ?? throw new NotFoundException("הבקרה לא נמצאה");
        return await RunAsync(record.Definition, record.Name, id, ct);
    }

    public Task<RunResult> PreviewAsync(ControlDefinition definition, CancellationToken ct) =>
        RunAsync(definition, "תצוגה מקדימה", null, ct);

    private async Task<RunResult> RunAsync(ControlDefinition definition, string name, int? controlId, CancellationToken ct)
    {
        var catalog = await catalogs.GetCatalogAsync(ct);
        var sw = Stopwatch.StartNew();
        try
        {
            var query = builder.Build(definition, catalog, new QueryOptions(MaxRows));
            SqlSafetyGuard.EnsureReadOnly(query.Sql);
            var raw = await executor.ExecuteAsync(query, ct);
            var (rows, total) = ResultMapper.Map(query, raw);
            sw.Stop();
            if (controlId is { } cid) await repo.RecordRunAsync(cid, total, sw.ElapsedMilliseconds, true, null, ct);

            return new RunResult(controlId, name,
                query.Columns.Select(c => new RunColumn(c.Key, c.Label, c.Type)).ToList(),
                rows, total, total > rows.Count, sw.ElapsedMilliseconds, query.Sql, DateTime.UtcNow);
        }
        catch (Exception ex) when (controlId is not null && ex is not OperationCanceledException)
        {
            sw.Stop();
            await repo.RecordRunAsync(controlId.Value, null, sw.ElapsedMilliseconds, false, Truncate(ex.Message), CancellationToken.None);
            throw;
        }
    }

    private static string Truncate(string s) => s.Length <= 900 ? s : s[..900];
}
