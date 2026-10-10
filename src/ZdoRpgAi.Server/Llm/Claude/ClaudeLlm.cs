using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Server.Util;
using ZdoRpgAi.Server.Catalog;

namespace ZdoRpgAi.Server.Llm.Claude;

// Claude over the Messages API with HttpClient and JsonNode, like the other providers: the official
// SDK serializes through reflection, which this Native AOT server can't do. The stable part of the
// prompt (tools and SystemPromptPrefix) is cached, and models that support it get a server-side
// fallback model, so a refused reply is re-served instead of the NPC falling silent.
public class ClaudeLlm : ILlm, IModelCatalog {
    private static readonly ILog Log = Logger.Get<ClaudeLlm>();
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private const int MaxTokens = 16000;
    private const string ApiVersion = "2023-06-01";

    // Per million tokens, in / out, as Anthropic listed them in October 2026: for choosing by budget
    private static readonly Dictionary<string, string> Prices = new() {
        ["claude-fable-5-1"] = "$10 / $50",
        ["claude-fable-5"] = "$10 / $50",
        ["claude-opus-5-5"] = "$4 / $20",
        ["claude-opus-5"] = "$5 / $25",
        ["claude-opus-4-8"] = "$5 / $25",
        ["claude-opus-4-7"] = "$5 / $25",
        ["claude-opus-4-6"] = "$5 / $25",
        ["claude-sonnet-5-5"] = "$2 / $10",
        ["claude-sonnet-5"] = "$2 / $10",
        ["claude-sonnet-4-6"] = "$3 / $15",
        ["claude-haiku-5-5"] = "$0.10 / $0.50",
        ["claude-haiku-4-5"] = "$1 / $5",
    };
    private const string FallbackBeta = "server-side-fallback-2026-07-01";

    // An NPC that takes minutes to answer is worse than one that doesn't
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(60) };
    private readonly ClaudeConfig _config;
    private readonly string _baseUrl;
    private readonly bool _serverFallback;

    public ClaudeLlm(ClaudeConfig config) {
        _config = config;
        _baseUrl = (config.BaseUrl ?? "https://api.anthropic.com").TrimEnd('/');
        // The "default" fallback form; Claude Haiku 5.5 has no server-side fallback
        _serverFallback = config.Model.StartsWith("claude-opus-5") || config.Model.StartsWith("claude-fable-5") || config.Model == "claude-sonnet-5-5";

        _http.DefaultRequestHeaders.Add("x-api-key", config.ApiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);
        if (_serverFallback) {
            _http.DefaultRequestHeaders.Add("anthropic-beta", FallbackBeta);
        }
    }

    // Newest first, as the Models API lists them
    public async Task<List<CatalogItem>> ListModelsAsync() {
        // Its own client: the fallback beta header belongs to messages only
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.Add("x-api-key", _config.ApiKey);
        http.DefaultRequestHeaders.Add("anthropic-version", ApiVersion);

        var items = new List<CatalogItem>();
        string? after = null;
        do {
            var url = $"{_baseUrl}/v1/models?limit=1000" + (after == null ? "" : $"&after_id={Uri.EscapeDataString(after)}");
            var resp = await http.GetAsync(url);
            var body = await resp.Content.ReadAsStringAsync();
            if (!resp.IsSuccessStatusCode) {
                throw new HttpRequestException(ApiError.Describe(resp.StatusCode, body));
            }

            var doc = JsonNode.Parse(body);
            foreach (var model in doc?["data"]?.AsArray() ?? []) {
                if (model?["id"]?.GetValue<string>() is { } id) {
                    items.Add(new CatalogItem(id, model["display_name"]?.GetValue<string>(), Price: Prices.GetValueOrDefault(id)));
                }
            }
            after = doc?["has_more"]?.GetValue<bool>() == true ? doc["last_id"]?.GetValue<string>() : null;
        } while (after != null);
        return items;
    }

    public async Task<LlmResponse> ChatAsync(LlmRequest request) {
        var body = new JsonObject {
            ["model"] = _config.Model,
            ["max_tokens"] = MaxTokens,
            ["system"] = BuildSystem(request),
            ["messages"] = BuildMessages(request.Messages),
        };
        if (!string.IsNullOrEmpty(_config.Effort)) {
            body["output_config"] = new JsonObject { ["effort"] = _config.Effort };
        }
        if (request.Tools.Count > 0) {
            body["tools"] = BuildTools(request.Tools, cacheLast: string.IsNullOrEmpty(request.SystemPromptPrefix));
            if (!request.AllowToolCalls) {
                body["tool_choice"] = new JsonObject { ["type"] = "none" };
            }
        }
        if (_serverFallback) {
            body["fallbacks"] = "default";
        }

        var json = ToJson(body);
        Log.Debug("Sending request ({MessageCount} messages, {ToolCount} tools) to {Model}",
            request.Messages.Count, request.Tools.Count, _config.Model);
        Log.Trace("Request body: {Body}", json);

        var resp = await _http.PostAsync($"{_baseUrl}/v1/messages", new StringContent(json, Encoding.UTF8, "application/json"));
        var respJson = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode) {
            Log.Error("API error {StatusCode}: {Response}", resp.StatusCode, respJson);
            return new LlmResponse { Error = ApiError.Describe(resp.StatusCode, respJson) };
        }

        Log.Trace("Raw response: {Response}", respJson);
        return ParseResponse(respJson);
    }

    private static JsonArray BuildSystem(LlmRequest request) {
        var blocks = new JsonArray();
        if (!string.IsNullOrEmpty(request.SystemPromptPrefix)) {
            // Tools render before the system prompt; with this prefix they form the cached part
            blocks.AddNode(TextBlock(request.SystemPromptPrefix, cache: true));
        }
        var system = BuildSystemText(request);
        if (system.Length > 0) {
            blocks.AddNode(TextBlock(system));
        }
        return blocks;
    }

    private static string BuildSystemText(LlmRequest request) {
        if (request.Resources.Count == 0) {
            return request.SystemPrompt;
        }

        var sb = new StringBuilder(request.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("# Available Resources");
        foreach (var resource in request.Resources) {
            sb.AppendLine();
            sb.AppendLine($"## {resource.Name}");
            if (!string.IsNullOrEmpty(resource.Description)) {
                sb.AppendLine(resource.Description);
            }
            sb.AppendLine();
            sb.AppendLine(resource.Content);
        }
        return sb.ToString();
    }

    private static JsonArray BuildMessages(List<LlmMessage> messages) {
        var result = new JsonArray();
        foreach (var msg in messages) {
            result.AddNode(new JsonObject {
                ["role"] = msg.Role == LlmRole.User ? "user" : "assistant",
                // A turn continued after its tool calls goes back unchanged, thinking signatures and all
                ["content"] = msg.ProviderContent is JsonArray original ? original.DeepClone() : BuildContent(msg),
            });
        }
        return result;
    }

    private static JsonArray BuildContent(LlmMessage msg) {
        var content = new JsonArray();
        if (!string.IsNullOrEmpty(msg.Text)) {
            content.AddNode(TextBlock(msg.Text));
        }
        foreach (var call in msg.ToolCalls ?? []) {
            content.AddNode(new JsonObject {
                ["type"] = "tool_use",
                ["id"] = call.Id,
                ["name"] = call.Name,
                ["input"] = call.Arguments.DeepClone(),
            });
        }
        foreach (var toolResult in msg.ToolResults ?? []) {
            content.AddNode(new JsonObject {
                ["type"] = "tool_result",
                ["tool_use_id"] = toolResult.CallId,
                ["content"] = toolResult.Result,
            });
        }
        return content;
    }

    private static JsonArray BuildTools(List<LlmTool> tools, bool cacheLast) {
        var arr = new JsonArray();
        for (var i = 0; i < tools.Count; i++) {
            var tool = tools[i];
            var props = new JsonObject();
            var required = new JsonArray();
            foreach (var p in tool.Parameters) {
                var schema = new JsonObject {
                    ["type"] = p.Type,
                    ["description"] = p.Description,
                };
                if (p.EnumValues is { Count: > 0 }) {
                    var enumArr = new JsonArray();
                    foreach (var v in p.EnumValues) {
                        enumArr.AddNode(JsonValue.Create(v)!);
                    }
                    schema["enum"] = enumArr;
                }
                props[p.Name] = schema;
                if (p.Required) {
                    required.AddNode(JsonValue.Create(p.Name)!);
                }
            }

            var definition = new JsonObject {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
                ["input_schema"] = new JsonObject {
                    ["type"] = "object",
                    ["properties"] = props,
                    ["required"] = required,
                },
            };
            if (cacheLast && i == tools.Count - 1) {
                definition["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
            }
            arr.AddNode(definition);
        }
        return arr;
    }

    private static LlmResponse ParseResponse(string respJson) {
        var doc = JsonNode.Parse(respJson);
        var usage = doc?["usage"];
        Log.Debug("Usage: {Input} input ({CacheRead} read from cache, {CacheWrite} written), {Output} output",
            usage?["input_tokens"]?.ToString() ?? "?", usage?["cache_read_input_tokens"]?.ToString() ?? "0",
            usage?["cache_creation_input_tokens"]?.ToString() ?? "0", usage?["output_tokens"]?.ToString() ?? "?");

        // A refusal arrives as HTTP 200; it is the fallback chain's final answer if one was set
        if (doc?["stop_reason"]?.GetValue<string>() == "refusal") {
            var category = doc["stop_details"]?["category"]?.ToString() ?? "unknown";
            Log.Warn("Claude declined to answer: {Category}", category);
            return new LlmResponse { Error = $"declined to answer ({category})" };
        }

        string? text = null;
        var toolCalls = new List<LlmToolCall>();
        var content = doc?["content"] as JsonArray;
        foreach (var block in content ?? []) {
            switch (block?["type"]?.GetValue<string>()) {
                case "text":
                    text = (text ?? "") + block["text"]?.GetValue<string>();
                    break;
                case "tool_use":
                    toolCalls.Add(new LlmToolCall {
                        Id = block["id"]!.GetValue<string>(),
                        Name = block["name"]!.GetValue<string>(),
                        Arguments = block["input"]?.DeepClone() as JsonObject ?? new JsonObject(),
                    });
                    break;
            }
        }

        return new LlmResponse {
            Text = text,
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
            ProviderContent = content?.DeepClone(),
        };
    }

    private static JsonObject TextBlock(string text, bool cache = false) {
        var block = new JsonObject { ["type"] = "text", ["text"] = text };
        if (cache) {
            block["cache_control"] = new JsonObject { ["type"] = "ephemeral" };
        }
        return block;
    }

    private static string ToJson(JsonNode node) {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions)) {
            node.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }
}
