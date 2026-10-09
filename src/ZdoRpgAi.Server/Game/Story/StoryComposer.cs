using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Game.Director;

namespace ZdoRpgAi.Server.Game.Story;

public class StoryComposer {
    private static readonly ILog Log = Logger.Get<StoryComposer>();

    private readonly Story _story;
    private readonly DirectorHelper _directorHelper;

    public StoryComposer(Story story, DirectorHelper directorHelper) {
        _story = story;
        _directorHelper = directorHelper;
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
