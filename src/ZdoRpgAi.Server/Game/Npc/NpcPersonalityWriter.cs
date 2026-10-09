using ZdoRpgAi.Core;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.Server.Game.Npc;

// Writes a short character sketch the first time an NPC talks, so that NPCs of the same class and
// race don't all sound alike. Generalizes the hand-written personalities in waterbobo24's fork.
public class NpcPersonalityWriter {
    private static readonly ILog Log = Logger.Get<NpcPersonalityWriter>();

    private readonly ILlm _llm;

    public NpcPersonalityWriter(ILlm llm) {
        _llm = llm;
    }

    public async Task<string?> WriteAsync(NpcInfo npc, string? cellName) {
        var who = $"{npc.Name}, a {npc.Sex.ToLowerInvariant()} {npc.Race}";
        if (!string.IsNullOrEmpty(npc.ClassName)) {
            who += $", {npc.ClassName}";
        }
        if (!string.IsNullOrEmpty(npc.Faction)) {
            who += string.IsNullOrEmpty(npc.FactionRank) ? $", member of the {npc.Faction}" : $", {npc.FactionRank} of the {npc.Faction}";
        }
        if (!string.IsNullOrEmpty(cellName)) {
            who += $", found in {cellName}";
        }

        var request = new LlmRequest {
            SystemPrompt = "You write character notes for NPCs of The Elder Scrolls III: Morrowind. Stay true to the lore of their race, class, faction and home. Never invent major plot facts.",
            Messages = [
                new LlmMessage {
                    Role = LlmRole.User,
                    Text = $"""
                        NPC: {who}.
                        In 3-4 short sentences describe their temperament, way of speaking, what they care about, and one small quirk or secret.
                        Address the NPC in the second person ("You are..."). Write in English. Reply with the notes only.
                        """,
                },
            ],
        };

        try {
            var response = await _llm.ChatAsync(request);
            var notes = response.Text?.Trim();
            return string.IsNullOrEmpty(notes) ? null : notes;
        }
        catch (Exception ex) {
            Log.Warn("Failed to write personality for {NpcId}: {Error}", npc.Id, ex.Message);
            return null;
        }
    }
}
