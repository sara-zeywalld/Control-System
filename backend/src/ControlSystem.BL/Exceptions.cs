namespace ControlSystem.BL;

/// <summary>הגדרה לא תקינה - ההודעות מיועדות להצגה למשתמש העסקי.</summary>
public sealed class ControlValidationException(IReadOnlyList<string> errors)
    : Exception(string.Join("; ", errors))
{
    public IReadOnlyList<string> Errors { get; } = errors;
}

public sealed class NotFoundException(string message) : Exception(message);

/// <summary>שאילתה שנחסמה ע"י מנגנון הבטיחות.</summary>
public sealed class UnsafeQueryException(string message) : Exception(message);

/// <summary>כשל בהרצה מול בסיס הנתונים; Message בטוחה להצגה, פרטים טכניים נרשמים ללוג.</summary>
public sealed class QueryExecutionException(string message, Exception? inner = null) : Exception(message, inner);

public sealed class AiProviderException(string message, Exception? inner = null) : Exception(message, inner);
