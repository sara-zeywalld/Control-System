using ControlSystem.BL.Controls;
using ControlSystem.BL.Definitions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ControlSystem.DAL.Persistence;

public sealed class SqlControlRepository(DatabaseOptions db) : IControlRepository
{
    private sealed record Row(int ControlId, string Name, string? Description, string DefinitionJson, string Source,
        DateTime CreatedAt, DateTime UpdatedAt, DateTime? RunAt, int? ViolationCount, bool? Succeeded, string? ErrorMessage);

    private const string SelectSql = """
        SELECT c.ControlId, c.Name, c.Description, c.DefinitionJson, c.Source, c.CreatedAt, c.UpdatedAt,
               r.RunAt, r.ViolationCount, r.Succeeded, r.ErrorMessage
        FROM ctl.Control AS c
        OUTER APPLY (SELECT TOP (1) * FROM ctl.ControlRun AS x WHERE x.ControlId = c.ControlId ORDER BY x.RunAt DESC) AS r
        WHERE c.IsDeleted = 0
        """;

    private SqlConnection Open() => new(db.Metadata);

    public async Task<IReadOnlyList<ControlRecord>> ListAsync(CancellationToken ct)
    {
        await using var conn = Open();
        var rows = await conn.QueryAsync<Row>(new CommandDefinition(SelectSql + " ORDER BY c.UpdatedAt DESC", cancellationToken: ct));
        return rows.Select(Map).ToList();
    }

    public async Task<ControlRecord?> GetAsync(int id, CancellationToken ct)
    {
        await using var conn = Open();
        var row = await conn.QuerySingleOrDefaultAsync<Row>(
            new CommandDefinition(SelectSql + " AND c.ControlId = @id", new { id }, cancellationToken: ct));
        return row is null ? null : Map(row);
    }

    public async Task<int> CreateAsync(ControlInput input, CancellationToken ct)
    {
        await using var conn = Open();
        return await conn.ExecuteScalarAsync<int>(new CommandDefinition("""
            INSERT ctl.Control (Name, Description, RootEntityKey, DefinitionJson, Source)
            OUTPUT INSERTED.ControlId
            VALUES (@Name, @Description, @Root, @Json, @Source)
            """, Params(input), cancellationToken: ct));
    }

    public async Task<bool> UpdateAsync(int id, ControlInput input, CancellationToken ct)
    {
        await using var conn = Open();
        var p = Params(input);
        p.Add("id", id);
        var affected = await conn.ExecuteAsync(new CommandDefinition("""
            UPDATE ctl.Control SET Name = @Name, Description = @Description, RootEntityKey = @Root,
                   DefinitionJson = @Json, UpdatedAt = SYSUTCDATETIME()
            WHERE ControlId = @id AND IsDeleted = 0
            """, p, cancellationToken: ct));
        return affected > 0;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        await using var conn = Open();
        var affected = await conn.ExecuteAsync(new CommandDefinition(
            "UPDATE ctl.Control SET IsDeleted = 1, UpdatedAt = SYSUTCDATETIME() WHERE ControlId = @id AND IsDeleted = 0",
            new { id }, cancellationToken: ct));
        return affected > 0;
    }

    public async Task RecordRunAsync(int controlId, int? count, long durationMs, bool succeeded, string? error, CancellationToken ct)
    {
        await using var conn = Open();
        await conn.ExecuteAsync(new CommandDefinition("""
            INSERT ctl.ControlRun (ControlId, ViolationCount, DurationMs, Succeeded, ErrorMessage)
            VALUES (@controlId, @count, @ms, @succeeded, @error)
            """, new { controlId, count, ms = (int)durationMs, succeeded, error }, cancellationToken: ct));
    }

    private static DynamicParameters Params(ControlInput i)
    {
        var p = new DynamicParameters();
        p.Add("Name", i.Name);
        p.Add("Description", i.Description);
        p.Add("Root", i.Definition.RootEntity);
        p.Add("Json", ControlJson.Serialize(i.Definition));
        p.Add("Source", i.Source ?? "manual");
        return p;
    }

    private static ControlRecord Map(Row r) => new(
        r.ControlId, r.Name, r.Description, ControlJson.Parse(r.DefinitionJson), r.Source, r.CreatedAt, r.UpdatedAt,
        r.RunAt is null ? null : new LastRunInfo(DateTime.SpecifyKind(r.RunAt.Value, DateTimeKind.Utc), r.ViolationCount, r.Succeeded == true, r.ErrorMessage));
}
