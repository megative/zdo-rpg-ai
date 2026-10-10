using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Game.Story;

namespace ZdoRpgAi.Server.Game.Director;

// Whoever follows the player talks on the road: a word on arriving in another town or region, and
// now and then when it's been quiet. Like banter, it waits for nobody else to be talking.
public class CompanionScheduler {
    private static readonly ILog Log = Logger.Get<CompanionScheduler>();

    private static readonly TimeSpan QuietBeforeTalk = TimeSpan.FromSeconds(45);
    // A companion keeps this close; it may take a moment to catch up after a door or a travel
    private const float NearMeters = 15f;
    private const int CatchUpTries = 5;
    private static readonly TimeSpan CatchUpDelay = TimeSpan.FromSeconds(1);
    private const string PlayerId = "player";

    private readonly Director _director;
    private readonly DirectorHelper _directorHelper;
    private readonly SimpleReactiveStrategy _strategy;
    private readonly ISaveGameRepository _saveGameRepo;
    private readonly StoryComposer _storyComposer;
    private readonly DirectorSection _config;

    private readonly object _lock = new();
    // The town or region of the last cell; the first one reported is where the game starts
    private string? _lastPlace;
    private bool _placeKnown;

    public CompanionScheduler(Director director, DirectorHelper directorHelper, SimpleReactiveStrategy strategy,
        ISaveGameRepository saveGameRepo, StoryComposer storyComposer, DirectorSection config) {
        _director = director;
        _directorHelper = directorHelper;
        _strategy = strategy;
        _saveGameRepo = saveGameRepo;
        _storyComposer = storyComposer;
        _config = config;
    }

    // Runs even with companions off, so turning them on in the settings takes effect at once
    public async Task RunAsync(CancellationToken ct) {
        try {
            while (!ct.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(_config.CompanionTalkIntervalSeconds), ct);
                if (!_config.Companions
                    || DateTime.UtcNow - _storyComposer.LastPlayerActivity < QuietBeforeTalk
                    || Random.Shared.NextDouble() >= _config.CompanionTalkChance) {
                    continue;
                }
                await TryTalkAsync(null);
            }
        }
        catch (OperationCanceledException) {
            // Server shutting down
        }
    }

    // A new town or region gets a word from a companion; doors within a town don't
    public void OnPlayerChangedCell(string playerId, string cell) {
        var place = RumorMill.TownOf(cell);
        lock (_lock) {
            var first = !_placeKnown;
            var same = place == _lastPlace;
            _placeKnown = true;
            _lastPlace = place;
            if (first || same || !_config.Companions) {
                return;
            }
        }
        _ = ArriveAsync(cell);
    }

    // A companion talks right away, unless someone else is talking: for scenarios and tests
    internal Task<bool> TryNowAsync(string? arrivedAt = null) => TryTalkAsync(arrivedAt);

    private async Task ArriveAsync(string cell) {
        try {
            await TryTalkAsync(cell);
        }
        catch (Exception ex) {
            Log.Warn("Companion arrival failed: {Error}", ex.Message);
        }
    }

    private async Task<bool> TryTalkAsync(string? arrivedAt) {
        try {
            var npcId = await FindCompanionNearAsync(arrivedAt != null ? CatchUpTries : 1);
            return npcId != null && await _director.TryRunExclusiveAsync(() => _strategy.CompanionLineAsync(npcId, PlayerId, arrivedAt));
        }
        catch (Exception ex) {
            Log.Warn("Companion talk failed: {Error}", ex.Message);
            return false;
        }
    }

    // A companion close to the player, waiting a little for one still catching up
    private async Task<string?> FindCompanionNearAsync(int tries) {
        var companions = _saveGameRepo.GetCompanions();
        for (var attempt = 0; companions.Length > 0 && attempt < tries; attempt++) {
            if (attempt > 0) {
                await Task.Delay(CatchUpDelay);
            }
            var around = await _directorHelper.QueryObserversAsync(PlayerId, null, NearMeters);
            var near = companions.Where(id => around.Ids.Contains(id, StringComparer.OrdinalIgnoreCase)).ToArray();
            if (near.Length > 0) {
                return near[Random.Shared.Next(near.Length)];
            }
        }
        Log.Debug("No companion close to the player");
        return null;
    }
}
