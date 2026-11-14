using System.Data;
using ControlSystem.BL;
using ControlSystem.BL.Catalog;
using ControlSystem.BL.Execution;
using ControlSystem.BL.Querying;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace ControlSystem.DAL.Persistence;

public sealed class SqlQueryExecutor(DatabaseOptions db, ILogger<SqlQueryExecutor> logger) : IQueryExecutor
{
    private const int TimeoutSeconds = 30;

    public async Task<RawResult> ExecuteAsync(BuiltQuery query, CancellationToken ct)
    {
        try
        {
            await using var conn = new SqlConnection(db.DataReadOnly);
            await conn.OpenAsync(ct);
            await using var cmd = new SqlCommand(query.Sql, conn) { CommandTimeout = TimeoutSeconds, CommandType = CommandType.Text };
            foreach (var p in query.Parameters) cmd.Parameters.Add(ToSqlParameter(p));

            var rows = new List<IReadOnlyDictionary<string, object?>>();
            await using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var row = new Dictionary<string, object?>(reader.FieldCount);
                for (var i = 0; i < reader.FieldCount; i++)
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(row);
            }
            return new RawResult(rows);
        }
        catch (SqlException ex)
        {
            logger.LogError(ex, "Control query failed. SQL: {Sql}", query.Sql);
            var message = ex.Number == -2
                ? "הרצת הבקרה ארכה זמן רב מדי ובוטלה. נסו לצמצם את התנאים."
                : "אירעה שגיאה בהרצת הבקרה מול בסיס הנתונים. פנו למנהל המערכת.";
            throw new QueryExecutionException(message, ex);
        }
    }

    private static SqlParameter ToSqlParameter(QueryParameter p) => p.Type switch
    {
        FieldType.Number => new SqlParameter(p.Name, SqlDbType.Decimal) { Precision = 18, Scale = 4, Value = p.Value },
        FieldType.Date => new SqlParameter(p.Name, SqlDbType.Date) { Value = p.Value },
        FieldType.Boolean => new SqlParameter(p.Name, SqlDbType.Bit) { Value = p.Value },
        _ => new SqlParameter(p.Name, SqlDbType.NVarChar, 4000) { Value = p.Value }
    };
}
