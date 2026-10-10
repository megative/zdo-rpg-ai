using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.SpeechToText;
using ZdoRpgAi.Server.TextToSpeech;
using ZdoRpgAi.Server.Util.Mp3;

namespace ZdoRpgAi.Server.Settings;

// Brings changed settings into the running server. New AI providers are swapped in; the director's
// and voice pacing settings are copied into the instances the game reads on each use. What the
// server reads only at start waits for a restart.
public class LiveSettings {
    private static readonly ILog Log = Logger.Get<LiveSettings>();

    // Read once at start
    public static readonly string[] RestartPaths = [
        "httpServer.host", "httpServer.port", "httpServer.maxMessageSize", "httpServer.rpcTimeoutMs", "database", "log",
    ];

    private readonly SwitchableLlm _mainLlm;
    private readonly SwitchableLlm _simpleLlm;
    private readonly SwitchableTextToSpeech _tts;
    private readonly SwitchableSpeechToText _stt;
    private readonly DirectorSection _director;
    private readonly Mp3SpeedConfig _mp3Speed;
    private readonly HttpServerSection _http;

    public LiveSettings(ServerConfig running, SwitchableLlm mainLlm, SwitchableLlm simpleLlm, SwitchableTextToSpeech tts, SwitchableSpeechToText stt) {
        _mainLlm = mainLlm;
        _simpleLlm = simpleLlm;
        _tts = tts;
        _stt = stt;
        _director = running.Director;
        _mp3Speed = running.Tts.Mp3Speed;
        _http = running.HttpServer;
    }

    // Creates the providers the new settings need, so one that can't start fails the change before
    // anything is written. oldJson and newJson are the configs as JSON.
    public Change Prepare(JsonObject oldJson, JsonObject newJson, ServerConfig next) {
        bool Changed(params string[] paths) => paths.Any(p => !JsonNode.DeepEquals(At(oldJson, p), At(newJson, p)));

        var change = new Change(next) {
            Director = Changed("director"),
            Mp3Speed = Changed("tts.mp3Speed"),
            ClientToken = Changed("httpServer.clientToken"),
        };
        try {
            change.MainLlm = Changed("llm.main") ? ServerBootstrap.CreateLlm("main", next.Llm.Main) : null;
            change.SimpleLlm = Changed("llm.simple") ? ServerBootstrap.CreateLlm("simple", next.Llm.Simple) : null;
            change.Tts = Changed("tts.provider", "tts.openAi", "tts.elevenLabs") ? ServerBootstrap.CreateTts(next.Tts) : null;
            change.Stt = Changed("stt") ? ServerBootstrap.CreateStt(next.Stt) : null;
        }
        catch {
            change.Discard();
            throw;
        }
        return change;
    }

    // Returns the sections now in effect
    public string[] Apply(Change change) {
        var applied = new List<string>();
        if (change.MainLlm != null) {
            _mainLlm.Swap(change.MainLlm);
            applied.Add("llm.main");
        }
        if (change.SimpleLlm != null) {
            _simpleLlm.Swap(change.SimpleLlm);
            applied.Add("llm.simple");
        }
        if (change.Tts != null) {
            _tts.Swap(change.Tts);
            applied.Add("tts");
        }
        if (change.Stt != null) {
            _stt.Swap(change.Stt);
            applied.Add("stt");
        }
        if (change.Mp3Speed) {
            _mp3Speed.CopyFrom(change.Next.Tts.Mp3Speed);
            applied.Add("tts.mp3Speed");
        }
        if (change.Director) {
            _director.CopyFrom(change.Next.Director);
            applied.Add("director");
        }
        if (change.ClientToken) {
            _http.ClientToken = change.Next.HttpServer.ClientToken;
            applied.Add("httpServer.clientToken");
        }

        if (applied.Count > 0) {
            Log.Info("Settings applied: {Sections}", string.Join(", ", applied));
        }
        return [.. applied];
    }

    internal static JsonNode? At(JsonNode? node, string path) {
        foreach (var key in path.Split('.')) {
            node = node is JsonObject obj ? obj[key] : null;
        }
        return node;
    }

    public class Change(ServerConfig next) {
        public ServerConfig Next { get; } = next;
        public ILlm? MainLlm { get; set; }
        public ILlm? SimpleLlm { get; set; }
        public ITextToSpeech? Tts { get; set; }
        public ISpeechToText? Stt { get; set; }
        public bool Director { get; init; }
        public bool Mp3Speed { get; init; }
        public bool ClientToken { get; init; }

        // When the change goes no further
        public void Discard() => Stt?.Dispose();
    }
}
