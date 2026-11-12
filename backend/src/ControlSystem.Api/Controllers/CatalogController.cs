using ControlSystem.Core.Catalog;
using ControlSystem.Core.Controls;
using ControlSystem.Core.Definitions;
using ControlSystem.Core.Querying;
using Microsoft.AspNetCore.Mvc;

namespace ControlSystem.Api.Controllers;

public sealed record OperatorOption(string Key, string Label);

/// <summary>הקטלוג העסקי כפי שנחשף ל-UI: רק מונחים עסקיים - בלי שמות טבלאות, עמודות או ביטויי SQL.</summary>
[ApiController]
[Route("api/catalog")]
public sealed class CatalogController(ICatalogProvider catalogs) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var catalog = await catalogs.GetCatalogAsync(ct);
        return Ok(new
        {
            entities = catalog.Entities.Select(e => new
            {
                e.Key, e.DisplayName, e.PluralName, e.Description, e.IsQueryable,
                fields = e.Fields.Select(f => new { f.Key, f.DisplayName, f.Description, type = f.Type, f.IsRequired }),
                relations = e.Relations.Select(r => new { r.Key, r.DisplayName, targetEntity = r.ToEntityKey })
            }),
            operators = Enum.GetValues<FieldType>().ToDictionary(
                t => t.ToString().ToLowerInvariant(),
                t => OperatorRules.AllowedFor(t).Select(o => new OperatorOption(
                    char.ToLowerInvariant(o.ToString()[0]) + o.ToString()[1..],
                    ConditionText.Operator(o, t).Trim().TrimEnd('-', ':').Trim())))
        });
    }
}

public sealed record ControlInputDto(string? Name, string? Description, ControlDefinition? Definition, string? Source);
