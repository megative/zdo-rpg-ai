namespace ZdoRpgAi.Server.SpeechToText;

public interface ISpeechToText : IDisposable {
    void Start();
    void FeedAudio(ReadOnlyMemory<byte> buffer);
    void Finish();
    void Cancel();

    // Sends the service a moment of silence; throws with the reason if it can't take it. For the
    // settings panel's connection test.
    Task TestAsync();

    event Action<string> InterimResultReceived;
    event Action<string> FinalResultReceived;
}
