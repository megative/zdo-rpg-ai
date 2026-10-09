using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Game.Director;

namespace ZdoRpgAi.Server.Game.Story;

public class StoryComposer {
    private static readonly ILog Log = Logger.Get<StoryComposer>();

    private readonly Story _story;
    private readonly DirectorHelper _directorHelper;
    private readonly ISaveGameRepository _saveGameRepo;

    public StoryComposer(Story story, DirectorHelper directorHelper, ISaveGameRepository saveGameRepo) {
        _story = story;
        _directorHelper = directorHelper;
        _saveGameRepo = saveGameRepo;
    }

    public void OnPlayerSpeak(string playerId, string? targetCharacterId, string gameTime, string text) {
        Log.Debug("OnPlayerSpeak");
        _ = OnPlayerSpeakAsync(playerId, targetCharacterId, gameTime, text);
    }

    private async Task OnPlayerSpeakAsync(string playerId, string? targetCharacterId, string gameTime, string text) {
        try {
            Log.Trace("Querying observers for player {PlayerId}", playerId);
            var observers = await _directorHelper.QueryObserversAsync(playerId, targetCharacterId != null ? [targetCharacterId] : null);
            Log.Trace("Got {Count} observers, registering event", observers.Ids.Length);

            // Every conversation starts with a player line, so switching here keeps memories in the
            // right playthrough even after a load or a server restart
            if (observers.SaveId != null && _saveGameRepo.SetSaveContext(observers.SaveId)) {
                Log.Info("Switched to the memory of save {SaveId}", observers.SaveId);
            }

            var evt = StoryEvent.Create(new StoryEvent.PlayerSpeak {
                PlayerCharacterId = playerId,
                TargetCharacterId = targetCharacterId,
                // The client can't see the game clock, the mod reports it along with the observers
                GameTime = observers.GameTime ?? gameTime,
                Text = text,
            });
            _story.RegisterEvent(evt, observers.Ids);
        }
        catch (Exception ex) {
            Log.Error("OnPlayerSpeakAsync failed: {Error}", ex.Message);
        }
    }
}
