using System.Diagnostics;
using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.TextToSpeech;

namespace ZdoRpgAi.Server.Settings;

// Ok: the service answered, in Ms. Reply: what the model said; Audio: the voice sample, MP3 in
// base64. Dummy: a stand-in provider, there is nothing to check.
public record ConnectionTestResult(bool Ok, long Ms, bool Dummy = false, string? Reply = null, string? Audio = null, string? Error = null);

// The settings panel's "Test" button: one small request to the AI service a part of the config uses,
// with the values in the form, saved or not
public class ConnectionTest {
    private static readonly ILog Log = Logger.Get<ConnectionTest>();
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    // target: llm.main, llm.simple, stt or tts
    public static async Task<ConnectionTestResult> RunAsync(ServerConfig config, string target) {
        var watch = Stopwatch.StartNew();
        ConnectionTestResult result;
        try {
            result = target switch {
                "llm.main" => await TestLlmAsync("main", config.Llm.Main, watch),
                "llm.simple" => await TestLlmAsync("simple", config.Llm.Simple, watch),
                "stt" => await TestSttAsync(config.Stt, watch),
                "tts" => await TestTtsAsync(config.Tts, config.Director.ResponseLanguage, watch),
                _ => new(false, 0, Error: $"nothing to test as {target}"),
            };
        }
        catch (TimeoutException) {
            result = new(false, watch.ElapsedMilliseconds, Error: $"no answer in {Timeout.TotalSeconds:0} s");
        }
        catch (Exception ex) {
            result = new(false, watch.ElapsedMilliseconds, Error: ex.Message);
        }

        Log.Info("Connection test of {Target}: {Result}", target, result.Ok ? $"ok in {result.Ms} ms" : result.Error ?? "failed");
        return result;
    }

    private static async Task<ConnectionTestResult> TestLlmAsync(string role, LlmProviderSection section, Stopwatch watch) {
        if (section.Provider == "dummy") {
            return new(true, 0, Dummy: true);
        }

        var response = await ServerBootstrap.CreateLlm(role, section, announce: false).ChatAsync(new LlmRequest {
            SystemPrompt = "This is a connection test. Reply with one short word.",
            Messages = [new LlmMessage { Role = LlmRole.User, Text = "Ping" }],
        }).WaitAsync(Timeout);
        var text = response.Text?.Trim();
        if (string.IsNullOrEmpty(text)) {
            return new(false, watch.ElapsedMilliseconds, Error: response.Error ?? "empty response");
        }
        return new(true, watch.ElapsedMilliseconds, Reply: text.Length > 80 ? text[..80] + "…" : text);
    }

    private static async Task<ConnectionTestResult> TestSttAsync(SttSection section, Stopwatch watch) {
        if (section.Provider == "dummy") {
            return new(true, 0, Dummy: true);
        }

        using var stt = ServerBootstrap.CreateStt(section, announce: false);
        await stt.TestAsync().WaitAsync(Timeout);
        return new(true, watch.ElapsedMilliseconds);
    }

    private static async Task<ConnectionTestResult> TestTtsAsync(TtsSection section, string language, Stopwatch watch) {
        if (section.Provider == "dummy") {
            return new(true, 0, Dummy: true);
        }

        var output = await ServerBootstrap.CreateTts(section, announce: false).GenerateAsync(new ITextToSpeechInput {
            npcId = "test",
            npcName = "Test",
            npcRace = "Dark Elf",
            npcSex = "Male",
            text = Sample(language),
        }).WaitAsync(Timeout);
        return new(true, watch.ElapsedMilliseconds, Audio: Convert.ToBase64String(output.Mp3Bytes));
    }

    // A line in the NPCs' language: some voices speak only some languages
    private static string Sample(string language) => language.Trim().ToLowerInvariant() switch {
        "russian" or "русский" => "Приветствую, чужеземец. Голос в порядке.",
        "ukrainian" => "Вітаю, чужинцю. Голос працює.",
        "belarusian" => "Вітаю, чужынца. Голас працуе.",
        "polish" => "Witaj, przybyszu. Głos działa.",
        "german" => "Sei gegrüßt, Fremder. Die Stimme funktioniert.",
        "french" => "Salutations, étranger. La voix fonctionne.",
        "spanish" => "Saludos, forastero. La voz funciona.",
        "italian" => "Salve, straniero. La voce funziona.",
        _ => "Greetings, outlander. The voice works.",
    };
}
