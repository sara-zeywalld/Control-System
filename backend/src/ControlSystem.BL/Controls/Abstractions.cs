using ControlSystem.BL.Catalog;
using ControlSystem.BL.Definitions;

namespace ControlSystem.BL.Controls;

public sealed record LastRunInfo(DateTime RanAtUtc, int? ViolationCount, bool Succeeded, string? ErrorMessage);

public sealed record ControlRecord(
    int Id, string Name, string? Description, ControlDefinition Definition,
    string Source, DateTime CreatedAt, DateTime UpdatedAt, LastRunInfo? LastRun);

public sealed record ControlInput(string Name, string? Description, ControlDefinition Definition, string? Source);

public interface ICatalogProvider
{
    Task<DataCatalog> GetCatalogAsync(CancellationToken ct);
}

public interface IControlRepository
{
    Task<IReadOnlyList<ControlRecord>> ListAsync(CancellationToken ct);
    Task<ControlRecord?> GetAsync(int id, CancellationToken ct);
    Task<int> CreateAsync(ControlInput input, CancellationToken ct);
    Task<bool> UpdateAsync(int id, ControlInput input, CancellationToken ct);
    Task<bool> DeleteAsync(int id, CancellationToken ct);
    Task RecordRunAsync(int controlId, int? count, long durationMs, bool succeeded, string? error, CancellationToken ct);
}
