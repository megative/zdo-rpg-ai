using System.Text.Json.Nodes;
using ZdoRpgAi.Tests.Support;
using ZdoRpgAi.Util;

namespace ZdoRpgAi.Tests;

// The settings panel edits the config file in place: only the setting changes, comments stay
public class YamlEditorTests {
    private static readonly string Example = File.ReadAllText(Path.Combine(TestSetup.RepoRoot, "example", "server-config.example.yaml"));

    private static string[] P(string dotted) => dotted.Split('.');

    private static JsonNode? At(string yaml, string dotted) {
        JsonNode? node = YamlToJsonTransformer.Parse(yaml);
        foreach (var part in P(dotted)) {
            if (node is not JsonObject obj) {
                return null;
            }
            node = obj.FirstOrDefault(kv => string.Equals(kv.Key, part, StringComparison.OrdinalIgnoreCase)).Value;
        }
        return node;
    }

    // Everything but the edited path parses the same
    private static void AssertSameExcept(string before, string after, string dotted) {
        var a = YamlToJsonTransformer.Parse(before);
        var b = YamlToJsonTransformer.Parse(after);
        Strip(a);
        Strip(b);
        Assert.True(JsonNode.DeepEquals(a, b), after);

        void Strip(JsonNode root) {
            var parts = P(dotted);
            JsonNode? node = root;
            for (var i = 0; i < parts.Length - 1 && node is JsonObject o; i++) {
                node = o.FirstOrDefault(kv => string.Equals(kv.Key, parts[i], StringComparison.OrdinalIgnoreCase)).Value;
            }
            if (node is JsonObject parent) {
                var key = parent.FirstOrDefault(kv => string.Equals(kv.Key, parts[^1], StringComparison.OrdinalIgnoreCase)).Key;
                if (key != null) {
                    parent.Remove(key);
                }
            }
        }
    }

    private static int ChangedLines(string a, string b) {
        var counts = a.Split('\n').GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
        var added = 0;
        foreach (var line in b.Split('\n')) {
            if (counts.TryGetValue(line, out var c) && c > 0) {
                counts[line] = c - 1;
            }
            else {
                added++;
            }
        }
        return added;
    }

    [Fact]
    public void ReplacesAValueInOneLineAndKeepsComments() {
        var r = YamlEditor.Set(Example, P("llm.main.provider"), JsonValue.Create("claude"));
        Assert.Equal("claude", At(r, "llm.main.provider")?.GetValue<string>());
        Assert.Equal(1, ChangedLines(Example, r));
        Assert.Contains("# LLM providers:", r);
        AssertSameExcept(Example, r, "llm.main.provider");
    }

    [Fact]
    public void AddsAMissingSectionUnderItsParent() {
        var r = YamlEditor.Set(Example, P("llm.main.provider"), JsonValue.Create("claude"));
        r = YamlEditor.Set(r, P("llm.main.claude.apiKey"), JsonValue.Create("sk-ant-test"));
        r = YamlEditor.Set(r, P("llm.main.claude.model"), JsonValue.Create("claude-opus-5-5"));
        Assert.Equal("sk-ant-test", At(r, "llm.main.claude.apiKey")?.GetValue<string>());
        Assert.Equal("claude-opus-5-5", At(r, "llm.main.claude.model")?.GetValue<string>());
        Assert.Contains("    provider: claude\n    claude:\n      apiKey: sk-ant-test\n      model: claude-opus-5-5\n", r);
        Assert.Equal("dummy", At(r, "llm.simple.provider")?.GetValue<string>());
    }

    [Fact]
    public void KeepsNumbersBooleansAndQuotedStrings() {
        var r = YamlEditor.Set(Example, P("director.banterChance"), JsonValue.Create(0.3));
        Assert.Contains("  banterChance: 0.3\n", r);
        r = YamlEditor.Set(r, P("director.banter"), JsonValue.Create(false));
        Assert.False(At(r, "director.banter")?.GetValue<bool>());
        r = YamlEditor.Set(r, P("director.playerBackstory"), JsonValue.Create("Бывший узник: \"Нереварин\" #1"));
        Assert.Equal("Бывший узник: \"Нереварин\" #1", At(r, "director.playerBackstory")?.GetValue<string>());
        Assert.Contains("  playerBackstory: \"Бывший узник: \\\"Нереварин\\\" #1\"\n", r);
        // A string that looks like a number stays a string
        r = YamlEditor.Set(r, P("llm.simple.provider"), JsonValue.Create("42"));
        Assert.Equal("42", At(r, "llm.simple.provider")?.GetValue<string>());
    }

    [Fact]
    public void InsertsSeveralMissingLevelsAndRemovesThemAgain() {
        var r = YamlEditor.Set(Example, P("tts.openAi.voiceMapping.byRaceSex.dm"), JsonValue.Create("onyx"));
        Assert.Equal("onyx", At(r, "tts.openAi.voiceMapping.byRaceSex.dm")?.GetValue<string>());
        Assert.Equal(15, At(r, "tts.mp3Speed.charactersPerSecond")?.GetValue<long>());
        Assert.Equal(Example, YamlEditor.Remove(r, P("tts.openAi.voiceMapping.byRaceSex.dm")));
    }

    [Fact]
    public void EditsFlowMappings() {
        var flow = "llm:\n  main:\n    provider: claude\n    claude: { apiKey: \"<key>\", model: claude-opus-5-5, effort: low }\n";
        var r = YamlEditor.Set(flow, P("llm.main.claude.effort"), JsonValue.Create("medium"));
        Assert.Equal(flow.Replace("effort: low", "effort: medium"), r);
        r = YamlEditor.Set(r, P("llm.main.claude.baseUrl"), JsonValue.Create("http://localhost:4000"));
        Assert.Contains("effort: medium, baseUrl: http://localhost:4000 }", r);
        r = YamlEditor.Remove(r, P("llm.main.claude.effort"));
        Assert.Null(At(r, "llm.main.claude.effort"));
        Assert.Equal("claude-opus-5-5", At(r, "llm.main.claude.model")?.GetValue<string>());
        r = YamlEditor.Remove(r, P("llm.main.claude.baseUrl"));
        Assert.Equal("llm:\n  main:\n    provider: claude\n    claude: { apiKey: \"<key>\", model: claude-opus-5-5 }\n", r);
        Assert.Equal(1, At(YamlEditor.Set("a: {}\n", P("a.b"), JsonValue.Create(1)), "a.b")?.GetValue<long>());
    }

    [Fact]
    public void FillsEmptyAndNullValues() {
        var r = YamlEditor.Set("httpServer:\n  host: 0.0.0.0\n  clientToken:\n  port: 8080\n", P("httpServer.clientToken"), JsonValue.Create("s3cret"));
        Assert.Equal("s3cret", At(r, "httpServer.clientToken")?.GetValue<string>());
        Assert.Equal(8080, At(r, "httpServer.port")?.GetValue<long>());
        var r2 = YamlEditor.Set("tts:\n  openAi:\n    voiceMapping:\n    # byRaceSex:\n    #   dm: onyx\n  provider: openai\n",
            P("tts.openAi.voiceMapping.byRaceSex.dm"), JsonValue.Create("onyx"));
        Assert.Equal("onyx", At(r2, "tts.openAi.voiceMapping.byRaceSex.dm")?.GetValue<string>());
        Assert.Equal("openai", At(r2, "tts.provider")?.GetValue<string>());
        Assert.Equal("x", At(YamlEditor.Set("director:\n  playerBackstory: null\n", P("director.playerBackstory"), JsonValue.Create("x")), "director.playerBackstory")?.GetValue<string>());
    }

    [Fact]
    public void KeepsCrlfAndAppendsNewSectionsAtTheEnd() {
        var crlf = Example.Replace("\n", "\r\n");
        var r = YamlEditor.Set(crlf, P("llm.main.claude.apiKey"), JsonValue.Create("k"));
        Assert.DoesNotContain('\n', r.Replace("\r\n", ""));
        Assert.Equal("k", At(r, "llm.main.claude.apiKey")?.GetValue<string>());
        Assert.EndsWith("eventLog:\n  key: L\n", YamlEditor.Set(Example, P("eventLog.key"), JsonValue.Create("L")));
        Assert.Equal("director:\n  banter: true\n", YamlEditor.Set("", P("director.banter"), JsonValue.Create(true)));
        Assert.Equal("# only a comment\ndirector:\n  banter: true\n", YamlEditor.Set("# only a comment\n", P("director.banter"), JsonValue.Create(true)));
    }

    [Fact]
    public void MatchesKeysWhateverTheCase() {
        var r = YamlEditor.Set("tts:\n  provider: openai\n  openai:\n    apiKey: a\n", P("tts.openAi.apiKey"), JsonValue.Create("b"));
        Assert.Equal("tts:\n  provider: openai\n  openai:\n    apiKey: b\n", r);
    }

    [Fact]
    public void RefusesToTurnASectionIntoAValue() =>
        Assert.Throws<InvalidOperationException>(() => YamlEditor.Set(Example, P("director"), JsonValue.Create("x")));

    [Fact]
    public void RemovesOnlyWhatIsThere() {
        Assert.Equal(Example, YamlEditor.Remove(Example, P("nope.nothing")));
        var r = YamlEditor.Remove(Example, P("tts.mp3Speed.charactersPerSecond"));
        Assert.Null(At(r, "tts.mp3Speed.charactersPerSecond"));
        Assert.Equal("ffmpeg", At(r, "tts.mp3Speed.ffmpegExePath")?.GetValue<string>());
    }
}
