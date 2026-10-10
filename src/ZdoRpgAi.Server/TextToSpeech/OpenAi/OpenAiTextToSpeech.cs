using System.Text;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Util;
using ZdoRpgAi.Server.Catalog;

namespace ZdoRpgAi.Server.TextToSpeech.OpenAi;

public class OpenAiTextToSpeech : ITextToSpeech, IModelCatalog {
    private static readonly ILog Log = Logger.Get<OpenAiTextToSpeech>();

    private readonly HttpClient _http = new();
    private readonly string _model;
    private readonly string _voice;
    private readonly string _baseUrl;
    private readonly Dictionary<string, string> _voiceByRaceSex;

    public OpenAiTextToSpeech(OpenAiTtsConfig config) {
        _model = config.Model;
        _voice = config.Voice;
        _baseUrl = config.BaseUrl.TrimEnd('/');
        _voiceByRaceSex = config.VoiceMapping?.ByRaceSex ?? new Dictionary<string, string>();
        // Fail fast on a stalled request instead of freezing the whole pipeline for the 100s default.
        _http.Timeout = TimeSpan.FromSeconds(20);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {config.ApiKey}");
    }

    public Task<List<CatalogItem>> ListModelsAsync() => OpenAiModelList.ListAsync(_http, _baseUrl, ModelTask.Speech);

    public async Task<ITextToSpeechOutput> GenerateAsync(ITextToSpeechInput input) {
        var voice = ResolveVoice(input.npcRace, input.npcSex);

        var url = $"{_baseUrl}/v1/audio/speech";

        var body = new JsonObject {
            ["model"] = _model,
            ["input"] = input.text,
            ["voice"] = voice,
            ["response_format"] = "mp3",
        };

        var content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");

        Log.Debug("Synthesizing {Length} chars with voice {Voice} for NPC {NpcId}", input.text.Length, voice, input.npcId);

        var resp = await _http.PostAsync(url, content);

        if (!resp.IsSuccessStatusCode) {
            var error = await resp.Content.ReadAsStringAsync();
            Log.Error("API error {StatusCode}: {Response}", resp.StatusCode, error);
            throw new HttpRequestException(ApiError.Describe(resp.StatusCode, error));
        }

        var audio = await resp.Content.ReadAsByteArrayAsync();
        Log.Debug("Received {Size} bytes of audio", audio.Length);
        return new ITextToSpeechOutput { Mp3Bytes = audio };
    }

    private string ResolveVoice(string race, string sex) {
        if (_voiceByRaceSex.Count == 0) {
            return _voice;
        }

        var key = $"{char.ToLowerInvariant(race[0])}{char.ToLowerInvariant(sex[0])}";
        if (_voiceByRaceSex.TryGetValue(key, out var voice)) {
            return voice;
        }

        Log.Warn("No voice mapping for key '{Key}' (race={Race}, sex={Sex}), using default", key, race, sex);
        return _voice;
    }
}
