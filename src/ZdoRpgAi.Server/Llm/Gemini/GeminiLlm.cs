using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Server.Util;
using ZdoRpgAi.Server.Catalog;

namespace ZdoRpgAi.Server.Llm.Gemini;

public class GeminiLlm : ILlm, IModelCatalog {
    private static readonly ILog Log = Logger.Get<GeminiLlm>();
    private static readonly JsonWriterOptions WriterOptions = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private readonly HttpClient _http = new();
    private readonly string _apiKey;
    private readonly string _model;
    private readonly int _thinkingBudget;

    public GeminiLlm(GeminiConfig config) {
        _apiKey = config.ApiKey;
        _model = config.Model;
        _thinkingBudget = config.ThinkingBudget;
    }

    public async Task<List<CatalogItem>> ListModelsAsync() {
        var resp = await _http.GetAsync($"https://generativelanguage.googleapis.com/v1beta/models?pageSize=1000&key={_apiKey}");
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) {
            throw new HttpRequestException(ApiError.Describe(resp.StatusCode, body));
        }

        var items = new List<CatalogItem>();
        foreach (var model in JsonNode.Parse(body)?["models"]?.AsArray() ?? []) {
            var name = model?["name"]?.GetValue<string>();
            var methods = model?["supportedGenerationMethods"]?.AsArray().Select(m => m?.GetValue<string>()) ?? [];
            if (name != null && methods.Contains("generateContent")) {
                items.Add(new CatalogItem(name.StartsWith("models/") ? name["models/".Length..] : name, model?["displayName"]?.GetValue<string>()));
            }
        }
        return items;
    }

    public async Task<LlmResponse> ChatAsync(LlmRequest request) {
        var url = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";

        var systemText = BuildSystemText(request);
        var body = new JsonObject {
            ["systemInstruction"] = new JsonObject {
                ["parts"] = new JsonArray { (JsonNode)new JsonObject { ["text"] = systemText } }
            },
            ["contents"] = BuildContents(request.Messages),
        };

        if (request.Tools.Count > 0) {
            body["tools"] = BuildTools(request.Tools);
            if (!request.AllowToolCalls) {
                body["toolConfig"] = new JsonObject {
                    ["functionCallingConfig"] = new JsonObject { ["mode"] = "NONE" }
                };
            }
        }

        body["generationConfig"] = new JsonObject {
            ["thinkingConfig"] = new JsonObject {
                ["thinkingBudget"] = _thinkingBudget
            }
        };

        var json = ToJson(body);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        Log.Debug("Sending request ({MessageCount} messages, {ToolCount} tools, {ResourceCount} resources)",
            request.Messages.Count, request.Tools.Count, request.Resources.Count);
        Log.Trace("Request body: {Body}", json);

        var resp = await _http.PostAsync(url, content);
        var respJson = await resp.Content.ReadAsStringAsync();

        if (!resp.IsSuccessStatusCode) {
            Log.Error("API error {StatusCode}: {Response}", resp.StatusCode, respJson);
            return new LlmResponse { Error = ApiError.Describe(resp.StatusCode, respJson) };
        }

        Log.Trace("Raw response: {Response}", respJson);

        return ParseResponse(respJson);
    }

    private static string BuildSystemText(LlmRequest request) {
        var system = string.IsNullOrEmpty(request.SystemPromptPrefix)
            ? request.SystemPrompt
            : $"{request.SystemPromptPrefix}\n\n{request.SystemPrompt}";
        if (request.Resources.Count == 0) {
            return system;
        }

        var sb = new StringBuilder(system);
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

    private static JsonArray BuildContents(List<LlmMessage> messages) {
        var contents = new JsonArray();
        foreach (var msg in messages) {
            var role = msg.Role == LlmRole.User ? "user" : "model";
            // Function calls carry thought signatures that must come back with them
            if (msg.ProviderContent is JsonArray original) {
                contents.AddNode(new JsonObject { ["role"] = role, ["parts"] = original.DeepClone() });
                continue;
            }

            var parts = new JsonArray();

            if (msg.Text != null) {
                parts.AddNode(new JsonObject { ["text"] = msg.Text });
            }

            if (msg.ToolCalls != null) {
                foreach (var tc in msg.ToolCalls) {
                    parts.AddNode(new JsonObject {
                        ["functionCall"] = new JsonObject { ["name"] = tc.Name, ["args"] = tc.Arguments.DeepClone() }
                    });
                }
            }

            if (msg.ToolResults != null) {
                foreach (var tr in msg.ToolResults) {
                    parts.AddNode(new JsonObject {
                        ["functionResponse"] = new JsonObject {
                            ["name"] = tr.Name,
                            ["response"] = new JsonObject { ["result"] = tr.Result }
                        }
                    });
                }
            }

            contents.AddNode(new JsonObject { ["role"] = role, ["parts"] = parts });
        }
        return contents;
    }

    private static JsonArray BuildTools(List<LlmTool> tools) {
        var funcDecls = new JsonArray();
        foreach (var tool in tools) {
            var decl = new JsonObject {
                ["name"] = tool.Name,
                ["description"] = tool.Description,
            };

            if (tool.Parameters.Count > 0) {
                var props = new JsonObject();
                var required = new JsonArray();
                foreach (var p in tool.Parameters) {
                    var paramObj = new JsonObject {
                        ["type"] = p.Type,
                        ["description"] = p.Description,
                    };
                    if (p.EnumValues is { Count: > 0 }) {
                        var enumArr = new JsonArray();
                        foreach (var v in p.EnumValues) {
                            enumArr.AddNode(JsonValue.Create(v)!);
                        }

                        paramObj["enum"] = enumArr;
                    }
                    props[p.Name] = paramObj;
                    if (p.Required) {
                        required.AddNode(JsonValue.Create(p.Name)!);
                    }
                }
                decl["parameters"] = new JsonObject {
                    ["type"] = "object",
                    ["properties"] = props,
                    ["required"] = required,
                };
            }

            funcDecls.AddNode(decl);
        }
        return new JsonArray { (JsonNode)new JsonObject { ["functionDeclarations"] = funcDecls } };
    }

    private static string ToJson(JsonObject body) {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions)) {
            body.WriteTo(writer);
        }
        return Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    private static LlmResponse ParseResponse(string respJson) {
        var doc = JsonNode.Parse(respJson);
        var parts = doc?["candidates"]?[0]?["content"]?["parts"];

        if (parts == null) {
            var blockReason = doc?["promptFeedback"]?["blockReason"]?.GetValue<string>();
            if (blockReason != null) {
                Log.Warn("Response blocked: {Reason}", blockReason);
            }
            else {
                Log.Warn("Empty response (no candidates)");
            }
            return new LlmResponse { Error = blockReason != null ? $"blocked ({blockReason})" : "empty response" };
        }

        string? text = null;
        var toolCalls = new List<LlmToolCall>();
        var callIndex = 0;

        foreach (var part in parts.AsArray()) {
            if (part?["text"] != null) {
                text = (text ?? "") + part["text"]!.GetValue<string>();
            }

            if (part?["functionCall"] != null) {
                var fc = part["functionCall"]!;
                toolCalls.Add(new LlmToolCall {
                    Id = $"call_{callIndex++}",
                    Name = fc["name"]!.GetValue<string>(),
                    Arguments = fc["args"]?.DeepClone() as JsonObject ?? new JsonObject(),
                });
            }
        }

        return new LlmResponse {
            Text = text,
            ToolCalls = toolCalls.Count > 0 ? toolCalls : null,
            ProviderContent = parts.DeepClone(),
        };
    }
}
