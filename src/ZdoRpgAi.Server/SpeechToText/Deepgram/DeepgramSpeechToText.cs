using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Util;
using ZdoRpgAi.Server.Catalog;

namespace ZdoRpgAi.Server.SpeechToText.Deepgram;

internal record DeepgramResult(DeepgramChannel Channel, bool IsFinal, bool SpeechFinal);
internal record DeepgramChannel(DeepgramAlternative[] Alternatives);
internal record DeepgramAlternative(string Transcript);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(DeepgramResult))]
internal partial class DeepgramJsonContext : JsonSerializerContext;

public class DeepgramSpeechToText : ISpeechToText, IModelCatalog {
    private static readonly ILog Log = Logger.Get<DeepgramSpeechToText>();
    private static readonly TimeSpan SessionTimeout = TimeSpan.FromSeconds(60);

    private readonly string _apiKey;
    private readonly int _sampleRate;
    private readonly string _encoding;
    private readonly string _language;
    private readonly string _model;

    private readonly object _lock = new();
    private ClientWebSocket? _ws;
    private Task? _sessionTask;
    private CancellationTokenSource? _sessionCts;
    private string _accumulatedTranscript = "";
    private List<byte[]>? _pendingBuffers;
    private bool _finishing;

    public event Action<string>? InterimResultReceived;
    public event Action<string>? FinalResultReceived;

    public DeepgramSpeechToText(DeepgramConfig config) {
        _apiKey = config.ApiKey;
        _sampleRate = config.SampleRate;
        _encoding = config.Encoding;
        _language = config.Language;
        _model = config.Model;
    }

    public void Start() {
        lock (_lock) {
            if (_sessionTask != null) {
                Log.Error("Start called while session is already active, ignoring");
                return;
            }

            Log.Debug("Starting Deepgram session (model={Model}, lang={Lang}, rate={Rate})",
                _model, _language, _sampleRate);
            _accumulatedTranscript = "";
            _pendingBuffers = new List<byte[]>();
            _finishing = false;
            _sessionCts = new CancellationTokenSource();
            _sessionTask = Task.Run(() => RunSessionAsync(_sessionCts.Token));
            Log.Debug("Create new sessionTask");
        }
    }

    public void FeedAudio(ReadOnlyMemory<byte> buffer) {
        lock (_lock) {
            if (_sessionTask == null) {
                Log.Warn("FeedAudio called without active session");
                return;
            }

            if (_pendingBuffers != null) {
                _pendingBuffers.Add(buffer.ToArray());
                Log.Trace("Buffered audio frame ({Bytes} bytes, {Count} pending)",
                    buffer.Length, _pendingBuffers.Count);
                return;
            }
        }

        var ws = _ws;
        if (ws is not { State: WebSocketState.Open }) {
            Log.Warn("FeedAudio: WebSocket not open (state={State})", (object?)ws?.State ?? "null");
            return;
        }
        Log.Trace("Sending audio frame ({Bytes} bytes)", buffer.Length);
        _ = SendAudioAsync(ws, buffer);
    }

    public void Finish() {
        lock (_lock) {
            if (_sessionTask == null) {
                Log.Warn("Finish called without active session");
                return;
            }
            _finishing = true;
        }

        Log.Debug("Finishing Deepgram session, sending CloseStream");
        var ws = _ws;
        if (ws is { State: WebSocketState.Open }) {
            _ = SendCloseStreamAsync(ws);
        }
        else {
            Log.Warn("Finish: WebSocket not open (state={State})", (object?)ws?.State ?? "null");
        }
    }

    // Newest first, the ones that know the configured language before the rest. Deepgram lists every
    // version of a model, each with some of its languages: they are put together.
    public async Task<List<CatalogItem>> ListModelsAsync() {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
        http.DefaultRequestHeaders.Add("Authorization", $"Token {_apiKey}");
        var resp = await http.GetAsync("https://api.deepgram.com/v1/models");
        var body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) {
            throw new HttpRequestException(ApiError.Describe(resp.StatusCode, body));
        }

        var models = new Dictionary<string, (string Architecture, HashSet<string> Languages)>();
        foreach (var model in JsonNode.Parse(body)?["stt"]?.AsArray() ?? []) {
            var id = model?["canonical_name"]?.GetValue<string>();
            if (id == null) {
                continue;
            }
            if (!models.TryGetValue(id, out var entry)) {
                entry = (model?["architecture"]?.GetValue<string>() ?? "", new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                models[id] = entry;
            }
            foreach (var language in model?["languages"]?.AsArray() ?? []) {
                if (language?.GetValue<string>() is { } code) {
                    entry.Languages.Add(code);
                }
            }
        }

        string[] newest = ["nova-3", "nova-2", "nova", "polaris", "whisper", "base"];
        int Age(string architecture) => Array.IndexOf(newest, architecture) is var i and >= 0 ? i : newest.Length;
        bool Speaks(HashSet<string> languages) => languages.Any(l => l.Equals("multi", StringComparison.OrdinalIgnoreCase)
            || l.Equals(_language, StringComparison.OrdinalIgnoreCase) || l.StartsWith(_language + "-", StringComparison.OrdinalIgnoreCase));

        var items = new List<CatalogItem>();
        // "nova-3" stands for its general model, and is what configs usually say
        foreach (var architecture in newest.Take(2)) {
            if (models.TryGetValue($"{architecture}-general", out var general)) {
                items.Add(new CatalogItem(architecture, Languages(general.Languages)));
            }
        }
        items.AddRange(models
            .OrderBy(m => Speaks(m.Value.Languages) ? 0 : 1)
            .ThenBy(m => Age(m.Value.Architecture))
            .ThenBy(m => m.Key.EndsWith("-general") ? 0 : 1)
            .ThenBy(m => m.Key, StringComparer.OrdinalIgnoreCase)
            .Select(m => new CatalogItem(m.Key, Languages(m.Value.Languages))));
        return items;
    }

    // "ru, en, de +30": the configured language first, if the model knows it
    private string? Languages(HashSet<string> languages) {
        var ordered = languages.OrderBy(l => l.StartsWith(_language, StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(l => l).ToList();
        return ordered.Count == 0 ? null : string.Join(", ", ordered.Take(4)) + (ordered.Count > 4 ? $" +{ordered.Count - 4}" : "");
    }

    // The same model and language through Deepgram's file endpoint, which needs no session
    public async Task TestAsync() {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        http.DefaultRequestHeaders.Add("Authorization", $"Token {_apiKey}");
        var content = new ByteArrayContent(Wav.Silence());
        content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/wav");
        var resp = await http.PostAsync($"https://api.deepgram.com/v1/listen?model={Uri.EscapeDataString(_model)}&language={Uri.EscapeDataString(_language)}", content);
        if (!resp.IsSuccessStatusCode) {
            throw new HttpRequestException(ApiError.Describe(resp.StatusCode, await resp.Content.ReadAsStringAsync()));
        }
    }

    public void Cancel() {
        CancellationTokenSource? cts;
        lock (_lock) {
            if (_sessionTask == null) {
                return;
            }

            cts = _sessionCts;
        }

        Log.Debug("Cancelling Deepgram session");
        cts?.Cancel();
    }

    public void Dispose() {
        Cancel();
        _sessionCts?.Dispose();
        _ws?.Dispose();
    }

    private async Task RunSessionAsync(CancellationToken ct) {
        using var timeoutCts = new CancellationTokenSource(SessionTimeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
        var token = linked.Token;

        try {
            var ws = new ClientWebSocket();
            ws.Options.SetRequestHeader("Authorization", $"Token {_apiKey}");

            var uri = $"wss://api.deepgram.com/v1/listen" +
                $"?encoding={_encoding}" +
                $"&sample_rate={_sampleRate}" +
                $"&language={_language}" +
                $"&model={_model}" +
                $"&interim_results=true" +
                $"&endpointing=300" +
                $"&vad_events=true";

            Log.Debug("Connecting to Deepgram WebSocket");
            await ws.ConnectAsync(new Uri(uri), token);
            Log.Debug("Deepgram WebSocket connected");
            _ws = ws;

            List<byte[]> buffered;
            lock (_lock) {
                buffered = _pendingBuffers!;
                _pendingBuffers = null;
            }

            foreach (var buf in buffered) {
                await ws.SendAsync(buf, WebSocketMessageType.Binary, true, token);
            }

            Log.Debug("Deepgram session started, flushed {Count} buffered frames", buffered.Count);

            await RunReceiveLoopAsync(ws, token);
            Log.Debug("Deepgram receive loop ended");
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested) {
            Log.Error("Speech recognition session exceeded {Timeout}s timeout, auto-cancelling",
                (int)SessionTimeout.TotalSeconds);
        }
        catch (OperationCanceledException) {
            Log.Debug("Deepgram session cancelled");
        }
        catch (WebSocketException ex) {
            Log.Warn("WebSocket error in STT session: {Error}", ex.Message);
        }
        catch (Exception ex) {
            Log.Error("Unexpected error in STT session: {Error}", ex.Message);
        }
        finally {
            bool wasFinishing;
            string transcript;
            lock (_lock) {
                wasFinishing = _finishing;
                transcript = _accumulatedTranscript.Trim();
                _sessionTask = null;
                Log.Debug("Set sessionTask to null");
                _pendingBuffers = null;
                _finishing = false;
            }

            Log.Debug("Deepgram session cleanup: wasFinishing={WasFinishing}, transcript='{Transcript}'",
                wasFinishing, transcript);
            CleanupWebSocket();

            if (wasFinishing && !string.IsNullOrEmpty(transcript)) {
                Log.Debug("Firing FinalResultReceived: '{Transcript}'", transcript);
                FinalResultReceived?.Invoke(transcript);
            }
            else if (wasFinishing) {
                Log.Warn("Session finished but no transcript accumulated");
            }
        }
    }

    private async Task RunReceiveLoopAsync(ClientWebSocket ws, CancellationToken ct) {
        var buffer = new byte[8192];
        while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested) {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do {
                result = await ws.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) {
                    Log.Debug("Deepgram WebSocket received close frame");
                    return;
                }
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            if (result.MessageType == WebSocketMessageType.Text) {
                var json = Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
                Log.Trace("Deepgram message: {Json}", json);
                ProcessMessage(json);
            }
        }
        Log.Debug("Receive loop exiting: wsState={State}, cancelled={Cancelled}",
            ws.State, ct.IsCancellationRequested);
    }

    private void ProcessMessage(string json) {
        try {
            var node = JsonNode.Parse(json);
            if (node == null) {
                return;
            }

            var type = node["type"]?.GetValue<string>();
            if (type != "Results") {
                Log.Trace("Deepgram non-result message: type={Type}", type ?? "null");
                return;
            }

            var result = node.Deserialize(DeepgramJsonContext.Default.DeepgramResult);
            if (result?.Channel?.Alternatives is not { Length: > 0 }) {
                return;
            }

            var transcript = result.Channel.Alternatives[0].Transcript;
            if (string.IsNullOrEmpty(transcript)) {
                return;
            }

            if (result.IsFinal) {
                _accumulatedTranscript += " " + transcript;
                Log.Debug("Deepgram final fragment: '{Transcript}', accumulated: '{Accumulated}'",
                    transcript, _accumulatedTranscript.Trim());
                InterimResultReceived?.Invoke(_accumulatedTranscript.Trim());
            }
            else {
                Log.Debug("Deepgram interim: '{Transcript}'", transcript);
                InterimResultReceived?.Invoke((_accumulatedTranscript + " " + transcript).Trim());
            }
        }
        catch (Exception ex) {
            Log.Warn("Error parsing Deepgram message: {Error}", ex.Message);
        }
    }

    private static async Task SendAudioAsync(ClientWebSocket ws, ReadOnlyMemory<byte> buffer) {
        try {
            if (ws.State == WebSocketState.Open) {
                await ws.SendAsync(buffer, WebSocketMessageType.Binary, true, CancellationToken.None);
            }
        }
        catch (WebSocketException ex) {
            Log.Warn("SendAudio failed: {Error}", ex.Message);
        }
    }

    private static async Task SendCloseStreamAsync(ClientWebSocket ws) {
        try {
            if (ws.State == WebSocketState.Open) {
                var closeMsg = "{\"type\":\"CloseStream\"}"u8.ToArray();
                await ws.SendAsync(closeMsg, WebSocketMessageType.Text, true, CancellationToken.None);
                Log.Debug("CloseStream message sent");
            }
        }
        catch (WebSocketException ex) {
            Log.Warn("SendCloseStream failed: {Error}", ex.Message);
        }
    }

    private void CleanupWebSocket() {
        var ws = _ws;
        _ws = null;
        if (ws == null) {
            return;
        }

        Log.Trace("Cleaning up WebSocket (state={State})", (object)ws.State);
        try {
            if (ws.State == WebSocketState.Open) {
                ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                    .ContinueWith(_ => ws.Dispose());
            }
            else {
                ws.Dispose();
            }
        }
        catch {
            ws.Dispose();
        }
    }
}
