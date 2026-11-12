using ControlSystem.Core;
using ControlSystem.Core.Ai;
using ControlSystem.Core.Controls;
using ControlSystem.Core.Definitions;
using Microsoft.AspNetCore.Mvc;

namespace ControlSystem.Api.Controllers;

[ApiController]
[Route("api/controls")]
public sealed class ControlsController(ControlService controls, ControlRunner runner) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct) => Ok(await controls.ListAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct) => Ok(await controls.GetAsync(id, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ControlInputDto dto, CancellationToken ct)
    {
        var created = await controls.CreateAsync(ToInput(dto), ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ControlInputDto dto, CancellationToken ct) =>
        Ok(await controls.UpdateAsync(id, ToInput(dto), ct));

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await controls.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpPost("{id:int}/duplicate")]
    public async Task<IActionResult> Duplicate(int id, CancellationToken ct) =>
        Ok(await controls.DuplicateAsync(id, ct));

    [HttpPost("{id:int}/run")]
    public async Task<IActionResult> Run(int id, CancellationToken ct) => Ok(await runner.RunSavedAsync(id, ct));

    /// <summary>הרצת הגדרה שטרם נשמרה (מהעורך / ממסך אישור ה-AI).</summary>
    [HttpPost("preview")]
    public async Task<IActionResult> Preview([FromBody] ControlDefinition definition, CancellationToken ct) =>
        Ok(await runner.PreviewAsync(definition, ct));

    private static ControlInput ToInput(ControlInputDto dto) =>
        new(dto.Name ?? "", dto.Description, dto.Definition!, dto.Source);
}

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
