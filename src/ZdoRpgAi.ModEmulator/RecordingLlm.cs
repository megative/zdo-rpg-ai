using System.Diagnostics;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.ModEmulator;

// A model call as it went: what was asked, what came back and how long it took
public record LlmCall(LlmRequest Request, LlmResponse Response, TimeSpan Took);

// Passes requests on and keeps them, so a scenario can check what an NPC was told and wait while
// the model thinks
public class RecordingLlm : ILlm {
    private readonly ILlm _inner;
    private readonly List<LlmCall> _calls = [];
    private int _inFlight;

    public RecordingLlm(ILlm inner) {
        _inner = inner;
    }

    public int InFlight => Volatile.Read(ref _inFlight);

    public IReadOnlyList<LlmCall> Calls {
        get {
            lock (_calls) {
                return _calls.ToList();
            }
        }
    }

    public async Task<LlmResponse> ChatAsync(LlmRequest request) {
        Interlocked.Increment(ref _inFlight);
        var started = Stopwatch.GetTimestamp();
        try {
            LlmResponse response;
            try {
                response = await _inner.ChatAsync(request);
            }
            catch (Exception ex) {
                response = new LlmResponse { Error = ex.Message };
                Record(request, response, started);
                throw;
            }
            Record(request, response, started);
            return response;
        }
        finally {
            Interlocked.Decrement(ref _inFlight);
        }
    }

    private void Record(LlmRequest request, LlmResponse response, long started) {
        lock (_calls) {
            _calls.Add(new LlmCall(request, response, Stopwatch.GetElapsedTime(started)));
        }
    }
}
