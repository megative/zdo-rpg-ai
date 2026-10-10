using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;

namespace ZdoRpgAi.Server.Game.Story;

// Keeps NPC memory in step with the loaded save: the playthrough by the save id the mod reports,
// the moment by the game clock. Loading an earlier save of the same playthrough turns the clock
// back, and whatever happened after that moment is forgotten, as it never happened in this save.
public sealed class SaveTimeline {
    private static readonly ILog Log = Logger.Get<SaveTimeline>();

    // The clock moves on between the moment a reply was made and the next look around
    private const double ToleranceSeconds = 60;

    private SaveTimeline() { }

    // Returns true when it switched to another playthrough
    public static bool Follow(ISaveGameRepository repo, string? saveId, double? gameSeconds) {
        var switched = saveId != null && repo.SetSaveContext(saveId);
        if (switched) {
            Log.Info("Switched to the memory of save {SaveId}", saveId);
        }

        if (gameSeconds is { } now) {
            var forgotten = repo.ForgetAfter(now + ToleranceSeconds);
            if (forgotten > 0) {
                Log.Info("An earlier save was loaded: NPCs forgot {Count} events that came after it", forgotten);
            }
        }
        return switched;
    }
}
