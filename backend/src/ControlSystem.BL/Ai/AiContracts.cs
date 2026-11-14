using ControlSystem.BL.Catalog;
using ControlSystem.BL.Controls;
using ControlSystem.BL.Definitions;
using ControlSystem.BL.Querying;

namespace ControlSystem.BL.Ai;

// ---- חוזה בין המערכת לבין מודל ה-AI (ספק חיצוני / שירות Python). גרסה: 1 ----

public sealed record AiCatalogField(string Key, string DisplayName, string? Description, string Type, IReadOnlyList<string> Synonyms, bool IsRequired);
public sealed record AiCatalogRelation(string Key, string DisplayName, string TargetEntity, string? Description, IReadOnlyList<string> Synonyms);
public sealed record AiCatalogEntity(
    string Key, string DisplayName, string PluralName, string? Description, IReadOnlyList<string> Synonyms,
    IReadOnlyList<AiCatalogField> Fields, IReadOnlyList<AiCatalogRelation> Relations);

/// <summary>מה נשלח למודל: הבקשה בשפה חופשית + תיאור עסקי של הנתונים + הפעולות המותרות לכל סוג שדה. ללא נתוני אמת.</summary>
public sealed record AiControlRequest(
    string Prompt,
    string Language,
    IReadOnlyList<AiCatalogEntity> Catalog,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedOperators,
    int SchemaVersion = 1);

/// <summary>מה מצופה לקבל מהמודל: הצעת בקרה במבנה של מנגנון הבקרות עצמו.</summary>
public sealed record AiControlProposal(
    string Name, string? Description, ControlDefinition Definition,
    string? Explanation, double Confidence, IReadOnlyList<string>? Warnings);

public sealed record AiProactiveRequest(
    IReadOnlyList<AiCatalogEntity> Catalog,
    IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedOperators,
    int SchemaVersion = 1);

public sealed record AiProactiveResponse(IReadOnlyList<AiControlProposal> Suggestions);

/// <summary>נקודת ההחלפה: כל מודל/ספק AI ממש את הממשק הזה בלבד. אין תלות בלוגיקת הרצת הבקרות.</summary>
public interface IAiControlProvider
{
    Task<AiControlProposal> SuggestControlAsync(AiControlRequest request, CancellationToken ct);
    Task<IReadOnlyList<AiControlProposal>> SuggestProactiveAsync(AiProactiveRequest request, CancellationToken ct);
}

// ---- מה שמוחזר ל-UI לאישור משתמש ----

public sealed record AiSuggestion(
    string Name, string? Description, ControlDefinition Definition, string Summary,
    string? Explanation, double Confidence, IReadOnlyList<string> Warnings, IReadOnlyList<string> ValidationErrors)
{
    public bool IsValid => ValidationErrors.Count == 0;
}

/// <summary>
/// מתווך בין ה-AI למנגנון הבקרות: בונה את ההקשר מהקטלוג, קורא לספק ומתרגם את התשובה להגדרת בקרה.
/// תשובת ה-AI אינה נחשבת אמינה - היא עוברת ולידציה מלאה ואינה נשמרת/מורצת לפני אישור המשתמש.
/// </summary>
public sealed class AiControlService(IAiControlProvider provider, ICatalogProvider catalogs)
{
    public async Task<AiSuggestion> SuggestAsync(string prompt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(prompt)) throw new ControlValidationException(["יש לתאר את הבקרה בטקסט חופשי"]);
        if (prompt.Length > 1000) throw new ControlValidationException(["התיאור ארוך מדי (עד 1000 תווים)"]);

        var catalog = await catalogs.GetCatalogAsync(ct);
        var request = new AiControlRequest(prompt.Trim(), "he", Describe(catalog), AllowedOperators());
        var proposal = await provider.SuggestControlAsync(request, ct);
        return Translate(proposal, catalog);
    }

    public async Task<IReadOnlyList<AiSuggestion>> SuggestProactivelyAsync(CancellationToken ct)
    {
        var catalog = await catalogs.GetCatalogAsync(ct);
        var proposals = await provider.SuggestProactiveAsync(new AiProactiveRequest(Describe(catalog), AllowedOperators()), ct);
        return proposals.Select(p => Translate(p, catalog)).ToList();
    }

    public static AiSuggestion Translate(AiControlProposal p, DataCatalog catalog)
    {
        var errors = ControlValidator.Validate(p.Definition, catalog);
        var summary = errors.Count == 0 ? ControlDescriber.Describe(p.Definition, catalog) : "";
        return new AiSuggestion(
            string.IsNullOrWhiteSpace(p.Name) ? "בקרה חדשה" : p.Name, p.Description, p.Definition, summary,
            p.Explanation, Math.Clamp(p.Confidence, 0, 1), p.Warnings ?? [], errors);
    }

    public static IReadOnlyList<AiCatalogEntity> Describe(DataCatalog catalog) =>
        catalog.Entities.Select(e => new AiCatalogEntity(
            e.Key, e.DisplayName, e.PluralName, e.Description, e.Synonyms,
            e.Fields.Select(f => new AiCatalogField(f.Key, f.DisplayName, f.Description, f.Type.ToString().ToLowerInvariant(), f.Synonyms, f.IsRequired)).ToList(),
            e.Relations.Select(r => new AiCatalogRelation(r.Key, r.DisplayName, r.ToEntityKey, r.Description, r.Synonyms)).ToList()))
            .ToList();

    public static IReadOnlyDictionary<string, IReadOnlyList<string>> AllowedOperators() =>
        Enum.GetValues<FieldType>().ToDictionary(
            t => t.ToString().ToLowerInvariant(),
            t => (IReadOnlyList<string>)OperatorRules.AllowedFor(t)
                .Select(o => char.ToLowerInvariant(o.ToString()[0]) + o.ToString()[1..]).ToList());
}
