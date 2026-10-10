namespace ZdoRpgAi.Server.Llm;

// The model a role uses right now: changed settings swap it while the server runs, and the next
// request goes to the new one
public class SwitchableLlm : ILlm {
    private volatile ILlm _current;

    public SwitchableLlm(ILlm initial) {
        _current = initial;
    }

    public void Swap(ILlm llm) => _current = llm;

    public Task<LlmResponse> ChatAsync(LlmRequest request) => _current.ChatAsync(request);
}
