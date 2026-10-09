using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Game.Npc;

namespace ZdoRpgAi.Server.Game.Story;

// What an NPC has heard around town: notable things NPCs did in the same town recently, such as
// handing out gold or picking a fight. "Town" is the part of the cell name before the comma, so
// "Seyda Neen, Arrille's Tradehouse" and the streets of "Seyda Neen" share their gossip.
public class RumorMill {
    private const int MaxRumors = 3;
    private const int Lookback = 30;

    private readonly ISaveGameRepository _saveGameRepo;
    private readonly NpcRepository _npcRepo;

    public RumorMill(ISaveGameRepository saveGameRepo, NpcRepository npcRepo) {
        _saveGameRepo = saveGameRepo;
        _npcRepo = npcRepo;
    }

    // With includeWitnessed false, only news to npcId: things it neither did nor saw itself
    public async Task<List<string>> GetRumorsAsync(string npcId, string? cell, bool includeWitnessed = false) {
        var town = TownOf(cell);
        if (town == null) {
            return [];
        }

        var rumors = new List<string>();
        var recent = _saveGameRepo.GetRecentEvents(nameof(StoryEvent.NpcAction), includeWitnessed ? null : npcId, Lookback);
        foreach (var raw in recent) {
            if (Story.DeserializeEvent(raw) is not StoryEvent.NpcAction action || action.Error != null || TownOf(action.Cell) != town) {
                continue;
            }

            var actor = await _npcRepo.GetNpcInfoAsync(action.NpcCharacterId);
            rumors.Add($"{actor?.Name ?? action.NpcCharacterId} {action.Description}");
            if (rumors.Count == MaxRumors) {
                break;
            }
        }
        return rumors;
    }

    public static string? TownOf(string? cell) {
        var town = cell?.Split(',')[0].Trim();
        return string.IsNullOrEmpty(town) ? null : town;
    }
}
