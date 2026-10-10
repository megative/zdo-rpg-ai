using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Game;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Server.Lua;
using ZdoRpgAi.Server.SpeechToText.Dummy;
using ZdoRpgAi.Server.TextToSpeech.Dummy;
using ZdoRpgAi.Server.Util.Mp3;

namespace ZdoRpgAi.ModEmulator;

// The server's game running in this process against the emulated game: no network and no audio,
// and databases in a temporary folder that goes away with the host
public sealed class GameHost : IAsyncDisposable {
    private static readonly ILog Log = Logger.Get<GameHost>();

    // How long the server stays silent, with no model call running, before a step counts as over
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(90);

    private readonly string _dir;
    private readonly CancellationTokenSource _cts = new();
    private readonly LocalDatabaseMainRepository _mainRepo;
    private readonly LocalDatabaseSaveGameRepository _saveRepo;
    private readonly InMemoryChannel _serverEnd;
    private readonly Task _running;

    private GameHost(string dir, WorldDefinition world, ILlm mainLlm, ILlm simpleLlm, DirectorSection director) {
        _dir = dir;
        Director = director;
        MainLlm = new RecordingLlm(mainLlm);
        SimpleLlm = new RecordingLlm(simpleLlm);
        _mainRepo = new LocalDatabaseMainRepository(Path.Combine(dir, "main.db"));
        _saveRepo = new LocalDatabaseSaveGameRepository(Path.Combine(dir, "save.db"));

        var (serverEnd, gameEnd) = InMemoryChannel.CreatePair();
        _serverEnd = serverEnd;
        var serverRpc = new RpcChannel(serverEnd);
        Runner = new GameRunner(_mainRepo, _saveRepo, serverRpc, new DummyTextToSpeech(), new DummySpeechToText(),
            MainLlm, SimpleLlm, new LuaSandbox(), director, new Mp3SpeedConfig());
        Game = new EmulatedGame(gameEnd, new EmulatedWorld(world));

        _running = Task.WhenAll(serverRpc.RunAsync(), Game.RunAsync(_cts.Token), Runner.RunAsync(_cts.Token));
    }

    public EmulatedGame Game { get; }
    public EmulatedWorld World => Game.World;
    public RecordingLlm MainLlm { get; }
    public RecordingLlm SimpleLlm { get; }
    // Live: a change takes effect on the next use, as when the settings change while the server runs
    public DirectorSection Director { get; }
    internal GameRunner Runner { get; }

    public bool Busy => MainLlm.InFlight > 0 || SimpleLlm.InFlight > 0;

    public static GameHost Start(WorldDefinition world, ILlm mainLlm, ILlm simpleLlm, DirectorSection? director = null) {
        var dir = Path.Combine(Path.GetTempPath(), "zdorpgai-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(dir);
        return new GameHost(dir, world, mainLlm, simpleLlm, director ?? new DirectorSection());
    }

    // Waits for the server to finish whatever the last step set off; false if it didn't in time
    public Task<bool> SettleAsync(TimeSpan? timeout = null) =>
        Game.WaitForQuietAsync(Quiet, timeout ?? DefaultTimeout, () => Busy);

    public async Task SayAsync(string text, TimeSpan? timeout = null) {
        Game.Say(text);
        await SettleAsync(timeout);
    }

    public async Task TargetAsync(string? npcId) {
        Game.SetTarget(npcId);
        await SettleAsync();
    }

    public async Task MoveAsync(string cell) {
        Game.MoveTo(cell);
        await SettleAsync();
    }

    // Two NPCs near the player talk now, without waiting for the quiet and the dice
    public async Task<bool> BanterAsync() {
        var started = await Runner.BanterNowAsync();
        await SettleAsync();
        return started;
    }

    // A companion close to the player says something, as on a quiet road
    public async Task<bool> CompanionTalkAsync() {
        var started = await Runner.CompanionTalkNowAsync();
        await SettleAsync();
        return started;
    }

    // Everything the NPC was told for each of its replies, oldest first: rules, its own
    // instructions and what it remembers
    public IReadOnlyList<string> PromptsOf(string npcId) => PromptsOf(npcId, MainLlm.Calls);

    public IReadOnlyList<string> PromptsOf(string npcId, IEnumerable<LlmCall> calls) {
        var name = World.FindNpc(npcId)?.Name ?? npcId;
        return calls
            .Where(c => c.Request.SystemPrompt.StartsWith($"You are {name},", StringComparison.Ordinal))
            .Select(c => TextOf(c.Request))
            .ToList();
    }

    public static string TextOf(LlmRequest request) =>
        string.Join("\n", new[] { request.SystemPromptPrefix, request.SystemPrompt }
            .Concat(request.Messages.Select(m => m.Text))
            .Where(t => !string.IsNullOrEmpty(t)));

    public async ValueTask DisposeAsync() {
        _cts.Cancel();
        _serverEnd.Close();
        try {
            await _running.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex is TimeoutException or OperationCanceledException) {
            Log.Debug("Game host stopped: {Error}", ex.Message);
        }
        _saveRepo.Dispose();
        _mainRepo.Dispose();
        _cts.Dispose();
        try {
            Directory.Delete(_dir, recursive: true);
        }
        catch (IOException ex) {
            Log.Warn("Could not remove {Dir}: {Error}", _dir, ex.Message);
        }
    }
}
