using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ZdoRpgAi.Server.Util;

public static class ApiError {
    // "401 Unauthorized: invalid x-api-key" from an error reply. Services put the reason in different
    // places: error.message (Claude, OpenAI, Gemini), error (Ollama), detail.message (ElevenLabs),
    // err_msg (Deepgram).
    public static string Describe(HttpStatusCode status, string body) {
        string? reason = null;
        try {
            var doc = JsonNode.Parse(body);
            reason = Text(doc?["error"]?["message"]) ?? Text(doc?["error"]) ?? Text(doc?["detail"]?["message"])
                ?? Text(doc?["detail"]) ?? Text(doc?["err_msg"]) ?? Text(doc?["message"]);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) {
            // Not JSON: the body itself says it, if anything
        }
        reason ??= body.Length > 200 ? body[..200].TrimEnd() + "…" : body.Trim();
        return reason.Length > 0 ? $"{(int)status} {status}: {reason}" : $"{(int)status} {status}";
    }

    private static string? Text(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;
}
