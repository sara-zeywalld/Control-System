using System.Text.Json;
using ControlSystem.Core;

namespace ControlSystem.Api.Middleware;

/// <summary>ממפה חריגות לתשובות HTTP אחידות (ProblemDetails) עם הודעות בעברית; פרטים טכניים נשארים בלוג.</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            context.Response.StatusCode = 499;
        }
        catch (Exception ex)
        {
            var (status, title, errors) = Map(ex);
            if (status >= 500) logger.LogError(ex, "Unhandled error");
            if (context.Response.HasStarted) throw;

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(new { status, title, errors });
        }
    }

    private static (int Status, string Title, IReadOnlyList<string> Errors) Map(Exception ex) => ex switch
    {
        ControlValidationException v => (400, "הגדרת הבקרה אינה תקינה", v.Errors),
        UnsafeQueryException u => (400, u.Message, [u.Message]),
        JsonException or BadHttpRequestException => (400, "הבקשה אינה תקינה", ["מבנה הבקשה אינו תקין"]),
        NotFoundException n => (404, n.Message, [n.Message]),
        AiProviderException a => (502, a.Message, [a.Message]),
        QueryExecutionException q => (500, q.Message, [q.Message]),
        _ => (500, "אירעה שגיאה לא צפויה", ["אירעה שגיאה לא צפויה. נסו שוב מאוחר יותר."])
    };
}
