using ControlSystem.BL.Ai;
using Microsoft.AspNetCore.Mvc;

namespace ControlSystem.Api.Controllers;

public sealed record AiSuggestRequest(string? Prompt);

[ApiController]
[Route("api/ai")]
public sealed class AiController(AiControlService ai) : ControllerBase
{
    /// <summary>מתרגם תיאור בשפה חופשית להצעת בקרה. לא שומר ולא מריץ - ממתין לאישור המשתמש.</summary>
    [HttpPost("suggest-control")]
    public async Task<IActionResult> Suggest([FromBody] AiSuggestRequest request, CancellationToken ct) =>
        Ok(await ai.SuggestAsync(request.Prompt ?? "", ct));

    /// <summary>הצעות יזומות לבקרות איכות נתונים על בסיס מבנה הקטלוג.</summary>
    [HttpPost("suggest-proactive")]
    public async Task<IActionResult> Proactive(CancellationToken ct) => Ok(await ai.SuggestProactivelyAsync(ct));
}
