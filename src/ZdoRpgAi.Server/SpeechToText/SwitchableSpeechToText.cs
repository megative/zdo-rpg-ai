namespace ZdoRpgAi.Server.SpeechToText;

// The speech recognition in use. One from changed settings takes over when the player next starts
// speaking, so a phrase still being recognized finishes with the old one.
public class SwitchableSpeechToText : ISpeechToText {
    private readonly object _lock = new();
    private ISpeechToText _current;
    private ISpeechToText? _next;
    // The one before the current may still deliver its last phrase
    private ISpeechToText? _previous;

    public event Action<string>? InterimResultReceived;
    public event Action<string>? FinalResultReceived;

    public SwitchableSpeechToText(ISpeechToText initial) {
        _current = Attach(initial);
    }

    public void Swap(ISpeechToText stt) {
        lock (_lock) {
            _next?.Dispose();
            _next = stt;
        }
    }

    public void Start() {
        ISpeechToText current;
        lock (_lock) {
            if (_next != null) {
                _previous?.Dispose();
                _previous = _current;
                _current = Attach(_next);
                _next = null;
            }
            current = _current;
        }
        current.Start();
    }

    public void FeedAudio(ReadOnlyMemory<byte> buffer) => Current.FeedAudio(buffer);
    public void Finish() => Current.Finish();
    public void Cancel() => Current.Cancel();
    public Task TestAsync() => Current.TestAsync();

    public void Dispose() {
        lock (_lock) {
            _next?.Dispose();
            _previous?.Dispose();
            _current.Dispose();
        }
    }

    private ISpeechToText Current {
        get {
            lock (_lock) {
                return _current;
            }
        }
    }

    private ISpeechToText Attach(ISpeechToText stt) {
        stt.InterimResultReceived += text => InterimResultReceived?.Invoke(text);
        stt.FinalResultReceived += text => FinalResultReceived?.Invoke(text);
        return stt;
    }
}
