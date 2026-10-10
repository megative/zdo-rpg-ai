using System.Text.Json.Nodes;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.Tests.Support;

// A stand-in model: the first rule that matches a request answers it. Anything else gets an empty
// answer, like a model with nothing to say.
public class ScriptedLlm : ILlm {
    private readonly List<(Func<LlmRequest, bool> When, Func<LlmRequest, LlmResponse> Reply)> _rules = [];

    public ScriptedLlm On(Func<LlmRequest, bool> when, Func<LlmRequest, LlmResponse> reply) {
        _rules.Add((when, reply));
        return this;
    }

    // What the NPC named so says, and the tools it calls, when asked for a reply
    public ScriptedLlm Npc(string name, string text, params (string Tool, JsonObject Args)[] calls) =>
        On(r => IsReplyOf(r, name), _ => Reply(text, calls));

    public ScriptedLlm Text(Func<LlmRequest, bool> when, string text) => On(when, _ => new LlmResponse { Text = text });

    public Task<LlmResponse> ChatAsync(LlmRequest request) {
        foreach (var (when, reply) in _rules) {
            if (when(request)) {
                return Task.FromResult(reply(request));
            }
        }
        return Task.FromResult(new LlmResponse { Text = "" });
    }

    public static bool IsReplyOf(LlmRequest request, string name) =>
        request.SystemPrompt.StartsWith($"You are {name},", StringComparison.Ordinal);

    public static LlmResponse Reply(string text, params (string Tool, JsonObject Args)[] calls) => new() {
        Text = text,
        ToolCalls = calls.Length == 0 ? null : calls.Select((c, i) => new LlmToolCall { Id = $"call{i}", Name = c.Tool, Arguments = c.Args }).ToList(),
    };
}
