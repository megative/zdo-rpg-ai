using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;

namespace ZdoRpgAi.Server.SpeechToText.OpenAi;

// Whisper is batch, not streaming: incoming PCM (linear16, 16 kHz, mono) is buffered while the
// session is active and transcribed in a single request on Finish().
public class OpenAiSpeechToText : ISpeechToText {
    private static readonly ILog Log = Logger.Get<OpenAiSpeechToText>();

    private const int SampleRate = 16_000;
    private const int Channels = 1;
    private const int BitsPerSample = 16;

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

    private async Task TranscribeAsync(byte[] pcm) {
        try {
            var wav = WrapWav(pcm);

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
                Log.Error("API error {StatusCode}: {Response}", resp.StatusCode, respJson);
                return;
            }

            Log.Trace("Raw response: {Response}", respJson);

            var text = JsonNode.Parse(respJson)?["text"]?.GetValue<string>()?.Trim();
            if (string.IsNullOrEmpty(text)) {
                Log.Warn("Transcription returned no text");
                return;
            }

            Log.Debug("Firing FinalResultReceived: '{Transcript}'", text);
            FinalResultReceived?.Invoke(text);
        }
        catch (Exception ex) {
            Log.Error("Unexpected error in OpenAI STT transcription: {Error}", ex.Message);
        }
    }

    private static byte[] WrapWav(byte[] pcm) {
        const int byteRate = SampleRate * Channels * BitsPerSample / 8;
        const short blockAlign = Channels * BitsPerSample / 8;
        var dataSize = pcm.Length;
        var riffSize = 36 + dataSize;

        using var ms = new MemoryStream(44 + dataSize);
        using var w = new BinaryWriter(ms, Encoding.ASCII, leaveOpen: true);

        w.Write("RIFF"u8);
        w.Write(riffSize);
        w.Write("WAVE"u8);

        w.Write("fmt "u8);
        w.Write(16);                          // fmt chunk size
        w.Write((short)1);                    // PCM
        w.Write((short)Channels);
        w.Write(SampleRate);
        w.Write(byteRate);
        w.Write(blockAlign);
        w.Write((short)BitsPerSample);

        w.Write("data"u8);
        w.Write(dataSize);
        w.Write(pcm);
        w.Flush();

        return ms.ToArray();
    }
}
