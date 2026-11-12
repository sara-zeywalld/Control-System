using System.Net.Http.Json;
using System.Text.Json;
using ControlSystem.Core;
using ControlSystem.Core.Ai;
using ControlSystem.Core.Definitions;
using Microsoft.Extensions.Logging;

namespace ControlSystem.Infrastructure.Ai;

public sealed class AiOptions
{
    public string BaseUrl { get; set; } = "http://localhost:8000";
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// מתאם לשירות ה-AI (Python) מעל HTTP/JSON לפי החוזה ב-AiContracts. כדי להחליף מודל/ספק:
/// ממשים IAiControlProvider אחר ורושמים אותו ב-DI - ללא שינוי בלוגיקת הבקרות.
/// </summary>
public sealed class HttpAiControlProvider(HttpClient http, ILogger<HttpAiControlProvider> logger) : IAiControlProvider
{
    public Task<AiControlProposal> SuggestControlAsync(AiControlRequest request, CancellationToken ct) =>
        PostAsync<AiControlRequest, AiControlProposal>("v1/control-suggestions", request, ct);

    public async Task<IReadOnlyList<AiControlProposal>> SuggestProactiveAsync(AiProactiveRequest request, CancellationToken ct) =>
        (await PostAsync<AiProactiveRequest, AiProactiveResponse>("v1/proactive-suggestions", request, ct)).Suggestions;

    private async Task<TResponse> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken ct)
    {
        try
        {
            using var response = await http.PostAsJsonAsync(path, body, ControlJson.Options, ct);
            if (!response.IsSuccessStatusCode)
            {
                var text = await response.Content.ReadAsStringAsync(ct);
                logger.LogWarning("AI service returned {Status}: {Body}", (int)response.StatusCode, text);
                throw new AiProviderException("שירות ה-AI החזיר שגיאה. נסו לנסח את הבקשה מחדש.");
            }
            return await response.Content.ReadFromJsonAsync<TResponse>(ControlJson.Options, ct)
                   ?? throw new AiProviderException("שירות ה-AI החזיר תשובה ריקה.");
        }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "AI service unreachable");
            throw new AiProviderException("שירות ה-AI אינו זמין כרגע. ודאו שהוא פועל (ראו הוראות הרצה).", ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new AiProviderException("שירות ה-AI לא הגיב בזמן.", ex);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "AI service returned an invalid payload");
            throw new AiProviderException("תשובת שירות ה-AI אינה במבנה המצופה.", ex);
        }
    }
}
