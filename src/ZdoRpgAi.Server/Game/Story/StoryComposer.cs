using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Game.Director;

namespace ZdoRpgAi.Server.Game.Story;

public class StoryComposer {
    private static readonly ILog Log = Logger.Get<StoryComposer>();

    // A greeting never cuts into a conversation that is still going on
    private static readonly TimeSpan QuietBeforeGreeting = TimeSpan.FromSeconds(20);

    private readonly Story _story;
    private readonly DirectorHelper _directorHelper;
    private readonly ISaveGameRepository _saveGameRepo;
    private readonly DirectorSection _config;

    private readonly object _lock = new();
    private DateTime _lastPlayerActivity = DateTime.MinValue;
    private readonly Dictionary<string, DateTime> _lastInteraction = new();

    public DateTime LastPlayerActivity {
        get {
            lock (_lock) {
                return _lastPlayerActivity;
            }
        }
    }

    public StoryComposer(Story story, DirectorHelper directorHelper, ISaveGameRepository saveGameRepo, DirectorSection config) {
        _story = story;
        _directorHelper = directorHelper;
        _saveGameRepo = saveGameRepo;
        _config = config;
    }

    public void OnPlayerSpeak(string playerId, string? targetCharacterId, string gameTime, string text) {
        Log.Debug("OnPlayerSpeak");
        lock (_lock) {
            _lastPlayerActivity = DateTime.UtcNow;
            if (targetCharacterId != null) {
                _lastInteraction[targetCharacterId] = DateTime.UtcNow;
            }
        }
        _ = OnPlayerSpeakAsync(playerId, targetCharacterId, gameTime, text);
    }

    // Looking at an NPC that remembers the player makes it greet them first. Throttled, so that
    // sweeping the crosshair over a crowd stays quiet.
    public void OnPlayerApproach(string playerId, string npcId) {
        if (!_config.Greetings) {
            return;
        }

        var now = DateTime.UtcNow;
        lock (_lock) {
            if (now - _lastPlayerActivity < QuietBeforeGreeting) {
                return;
            }
            if (_lastInteraction.TryGetValue(npcId, out var last) && now - last < TimeSpan.FromMinutes(_config.GreetingCooldownMinutes)) {
                return;
            }
            _lastInteraction[npcId] = now;
        }
        _ = OnPlayerApproachAsync(playerId, npcId);
    }

    private async Task OnPlayerSpeakAsync(string playerId, string? targetCharacterId, string gameTime, string text) {
        try {
            Log.Trace("Querying observers for player {PlayerId}", playerId);
            var observers = await QueryObserversAsync(playerId, targetCharacterId);
            Log.Trace("Got {Count} observers, registering event", observers.Ids.Length);

            var evt = StoryEvent.Create(new StoryEvent.PlayerSpeak {
                PlayerCharacterId = playerId,
                TargetCharacterId = targetCharacterId,
                // The client can't see the game clock, the mod reports it along with the observers
                GameTime = observers.GameTime ?? gameTime,
                GameSeconds = observers.GameSeconds,
                Cell = observers.Cell,
                Text = text,
            });
            _story.RegisterEvent(evt, observers.Ids);
        }
        catch (Exception ex) {
            Log.Error("OnPlayerSpeakAsync failed: {Error}", ex.Message);
        }
    }

    private async Task OnPlayerApproachAsync(string playerId, string npcId) {
        try {
            var observers = await QueryObserversAsync(playerId, npcId);

            // Strangers get the game's own hello; only NPCs with memories of the player speak up
            var remembers = _saveGameRepo.GetActiveStoryEventsForCharacter(npcId).Count > 0
                         || _saveGameRepo.GetActiveSummariesForCharacter(npcId).Count > 0;
            if (!remembers) {
                return;
            }

            lock (_lock) {
                _lastPlayerActivity = DateTime.UtcNow;
            }
            Log.Info("Player approached {NpcId}, who remembers them", npcId);
            var evt = StoryEvent.Create(new StoryEvent.PlayerApproach {
                PlayerCharacterId = playerId,
                NpcCharacterId = npcId,
                GameTime = observers.GameTime ?? "0",
                GameSeconds = observers.GameSeconds,
                Cell = observers.Cell,
            });
            _story.RegisterEvent(evt, observers.Ids);
        }
        catch (Exception ex) {
            Log.Error("OnPlayerApproachAsync failed: {Error}", ex.Message);
        }
    }

    private async Task<Observers> QueryObserversAsync(string playerId, string? targetCharacterId) {
        var observers = await _directorHelper.QueryObserversAsync(playerId, targetCharacterId != null ? [targetCharacterId] : null);

        // Every story starts with the player, so switching here keeps memories in the right
        // playthrough even after a load or a server restart
        SaveTimeline.Follow(_saveGameRepo, observers.SaveId, observers.GameSeconds);
        return observers;
    }
}
