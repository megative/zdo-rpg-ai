namespace ZdoRpgAi.Server.TextToSpeech;

// The voices in use right now: changed settings swap them while the server runs
public class SwitchableTextToSpeech : ITextToSpeech {
    private volatile ITextToSpeech _current;

    public SwitchableTextToSpeech(ITextToSpeech initial) {
        _current = initial;
    }

    public void Swap(ITextToSpeech tts) => _current = tts;

    public Task<ITextToSpeechOutput> GenerateAsync(ITextToSpeechInput input) => _current.GenerateAsync(input);
}
