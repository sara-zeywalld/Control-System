using System.Text.RegularExpressions;

namespace ControlSystem.Core.Querying;

/// <summary>
/// שכבת הגנה נוספת (defense in depth): השאילתות נוצרות רק ע"י ה-QueryBuilder, אך לפני הרצה מוודאים
/// שמדובר בפקודת SELECT בודדת בלי פעולות שינוי. ההגנה העיקרית: פרמטרים + מזהים מהקטלוג + משתמש קריאה בלבד.
/// </summary>
public static partial class SqlSafetyGuard
{
    [GeneratedRegex(@"N?'(?:[^']|'')*'")]
    private static partial Regex StringLiterals();

    [GeneratedRegex(@"\b(INSERT|UPDATE|DELETE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|MERGE|GRANT|REVOKE|DENY|INTO|OPENROWSET|OPENQUERY|OPENDATASOURCE|BULK|WAITFOR|SHUTDOWN|DBCC|BACKUP|RESTORE|USE|SET|DECLARE)\b|\bxp_|\bsp_|;|--|/\*", RegexOptions.IgnoreCase)]
    private static partial Regex Forbidden();

    public static void EnsureReadOnly(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql) || !sql.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
            throw new UnsafeQueryException("מותרות רק שאילתות קריאה (SELECT)");

        var withoutLiterals = StringLiterals().Replace(sql, "''");
        var match = Forbidden().Match(withoutLiterals);
        if (match.Success)
            throw new UnsafeQueryException($"השאילתה נחסמה מטעמי אבטחה ('{match.Value}')");
    }
}
