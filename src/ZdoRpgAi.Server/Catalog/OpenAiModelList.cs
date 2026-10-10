using System.Globalization;
using System.Text.Json.Nodes;
using ZdoRpgAi.Server.Util;

namespace ZdoRpgAi.Server.Catalog;

public enum ModelTask { Chat, Transcription, Speech }

// The models behind an OpenAI-compatible {baseUrl}/v1/models. OpenAI itself lists everything it has,
// so only the models for the task are kept. A local server (Ollama, LM Studio, vLLM, llama.cpp,
// speaches) lists what is installed; Ollama also tells each model's size.
public static class OpenAiModelList {
    private static readonly string[] NotChat = ["audio", "realtime", "transcribe", "tts", "image", "search", "embedding", "moderation", "instruct"];

    public static async Task<List<CatalogItem>> ListAsync(HttpClient http, string baseUrl, ModelTask task) {
        var resp = await http.GetAsync($"{baseUrl}/v1/models");
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) {
            throw new HttpRequestException(ApiError.Describe(resp.StatusCode, body));
        }

        var official = Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri) && uri.Host == "api.openai.com";
        var models = new List<(string Id, long Created)>();
        foreach (var model in JsonNode.Parse(body)?["data"]?.AsArray() ?? []) {
            var id = model?["id"]?.GetValue<string>();
            if (id == null || !Fits(id, model?["task"]?.GetValue<string>(), task, official)) {
                continue;
            }
            models.Add((id, model?["created"] is JsonValue created && created.TryGetValue<long>(out var at) ? at : 0));
        }

        var sizes = official ? new Dictionary<string, string>() : await OllamaSizesAsync(http, baseUrl);
        var ordered = official ? models.OrderByDescending(m => m.Created) : models.OrderBy(m => m.Id, StringComparer.OrdinalIgnoreCase);
        return [.. ordered.Select(m => new CatalogItem(m.Id, Size: sizes.GetValueOrDefault(m.Id)))];
    }

    // serverTask: what a local speech server says the model does, if it says
    private static bool Fits(string id, string? serverTask, ModelTask task, bool official) {
        var lower = id.ToLowerInvariant();
        if (official) {
            return task switch {
                ModelTask.Chat => (lower.StartsWith("gpt-") || lower.StartsWith("chatgpt-") || lower.StartsWith("o1") || lower.StartsWith("o3") || lower.StartsWith("o4"))
                    && !NotChat.Any(lower.Contains),
                ModelTask.Transcription => lower.Contains("whisper") || lower.Contains("transcribe"),
                _ => lower.Contains("tts"),
            };
        }
        return task switch {
            ModelTask.Chat => !lower.Contains("embed"),
            ModelTask.Transcription => serverTask == null || serverTask.Contains("speech-recognition"),
            _ => serverTask == null || serverTask == "text-to-speech",
        };
    }

    // "7.6B · Q4_K_M · 4.4 GB" by model name, from Ollama's own list; other servers have none
    private static async Task<Dictionary<string, string>> OllamaSizesAsync(HttpClient http, string baseUrl) {
        var sizes = new Dictionary<string, string>();
        try {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var resp = await http.GetAsync($"{baseUrl}/api/tags", cts.Token);
            if (!resp.IsSuccessStatusCode) {
                return sizes;
            }
            foreach (var model in JsonNode.Parse(await resp.Content.ReadAsStringAsync(cts.Token))?["models"]?.AsArray() ?? []) {
                var name = model?["name"]?.GetValue<string>();
                if (name == null) {
                    continue;
                }
                var details = model?["details"];
                var parts = new List<string>();
                if (details?["parameter_size"]?.GetValue<string>() is { Length: > 0 } parameters) {
                    parts.Add(parameters);
                }
                if (details?["quantization_level"]?.GetValue<string>() is { Length: > 0 } quantization) {
                    parts.Add(quantization);
                }
                if (model?["size"] is JsonValue size && size.TryGetValue<long>(out var bytes) && bytes > 0) {
                    parts.Add(string.Create(CultureInfo.InvariantCulture, $"{bytes / 1e9:0.0} GB"));
                }
                if (parts.Count > 0) {
                    sizes[name] = string.Join(" · ", parts);
                }
            }
        }
        catch (Exception) {
            // Not Ollama, or slow to answer: the list goes without sizes
        }
        return sizes;
    }
}
