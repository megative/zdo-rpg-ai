using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Util;
using ZdoRpgAi.Server.Catalog;

namespace ZdoRpgAi.Server.SpeechToText.OpenAi;

// Whisper is batch, not streaming: incoming PCM (linear16, 16 kHz, mono) is buffered while the
// session is active and transcribed in a single request on Finish().
public class OpenAiSpeechToText : ISpeechToText, IModelCatalog {
    private static readonly ILog Log = Logger.Get<OpenAiSpeechToText>();

    private readonly HttpClient _http = new();
    private readonly string _apiKey;
    private readonly string _model;
    private readonly string? _language;
    private readonly string _baseUrl;

    private readonly object _lock = new();
    private MemoryStream? _buffer;

    public event Action<string>? InterimResultReceived { add { } remove { } }
    public event Action<string>? FinalResultReceived;

    public OpenAiSpeechToText(OpenAiSttConfig config) {
        _apiKey = config.ApiKey;
        _model = config.Model;
        _language = config.Language;
        _baseUrl = config.BaseUrl.TrimEnd('/');
        // Fail fast on a stalled request instead of freezing the pipeline for the 100s default.
        _http.Timeout = TimeSpan.FromSeconds(30);
        _http.DefaultRequestHeaders.Add("Authorization", $"Bearer {_apiKey}");
    }

    public void Start() {
        lock (_lock) {
            Log.Debug("Starting OpenAI STT session (model={Model}, lang={Lang})", _model, _language ?? "auto");
            _buffer = new MemoryStream();
        }
    }

    public void FeedAudio(ReadOnlyMemory<byte> buffer) {
        lock (_lock) {
            if (_buffer == null) {
                Log.Warn("FeedAudio called without active session");
                return;
            }
            _buffer.Write(buffer.Span);
            Log.Trace("Buffered audio frame ({Bytes} bytes, {Total} total)", buffer.Length, _buffer.Length);
        }
    }

    public void Finish() {
        byte[] pcm;
        lock (_lock) {
            if (_buffer == null) {
                Log.Warn("Finish called without active session");
                return;
            }
            pcm = _buffer.ToArray();
            _buffer = null;
        }

        if (pcm.Length == 0) {
            Log.Warn("Finish called but no audio was buffered");
            return;
        }

        Log.Debug("Finishing OpenAI STT session, transcribing {Bytes} bytes of PCM", pcm.Length);
        _ = TranscribeAsync(pcm);
    }

    public void Cancel() {
        lock (_lock) {
            if (_buffer == null) {
                return;
            }
            Log.Debug("Cancelling OpenAI STT session, discarding buffered audio");
            _buffer = null;
        }
    }

    public void Dispose() {
        Cancel();
        _http.Dispose();
    }

    public Task TestAsync() => RequestTextAsync(Wav.Silence());

    public Task<List<CatalogItem>> ListModelsAsync() => OpenAiModelList.ListAsync(_http, _baseUrl, ModelTask.Transcription);

    private async Task TranscribeAsync(byte[] pcm) {
        try {
            var text = await RequestTextAsync(Wav.FromPcm(pcm));
            if (string.IsNullOrEmpty(text)) {
                Log.Warn("Transcription returned no text");
                return;
            }

            Log.Debug("Firing FinalResultReceived: '{Transcript}'", text);
            FinalResultReceived?.Invoke(text);
        }
        catch (Exception ex) {
            Log.Error("OpenAI STT transcription failed: {Error}", ex.Message);
        }
    }

    // The recognized text; an API error throws with its reason
    private async Task<string?> RequestTextAsync(byte[] wav) {
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(wav);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");
        form.Add(fileContent, "file", "audio.wav");
        form.Add(new StringContent(_model), "model");
        if (!string.IsNullOrEmpty(_language)) {
            form.Add(new StringContent(_language), "language");
        }

        var url = $"{_baseUrl}/v1/audio/transcriptions";
        Log.Debug("Sending transcription request ({Bytes} byte WAV) to {Url}", wav.Length, url);

        var resp = await _http.PostAsync(url, form);
        var respJson = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode) {
            throw new HttpRequestException(ApiError.Describe(resp.StatusCode, respJson));
        }

        Log.Trace("Raw response: {Response}", respJson);
        return JsonNode.Parse(respJson)?["text"]?.GetValue<string>()?.Trim();
    }
}
