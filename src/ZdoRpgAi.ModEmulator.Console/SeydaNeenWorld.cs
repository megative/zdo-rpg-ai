using ZdoRpgAi.Protocol.Messages;

namespace ZdoRpgAi.ModEmulator.Console;

public record NpcDefinition(
    string ObjectId, string Name, string Race, string Sex, float X, float Y,
    string ClassName, string? Faction = null, string? FactionRank = null, int Level = 1, int Gold = 0,
    string[]? Equipped = null, SceneItem[]? Items = null);

public static class SeydaNeenWorld {
    public const string CellName = "Seyda Neen";
    public const string PlayerId = "player";
    public const string GameTime = "09:30, 16 Last Seed 3E 427";

    public static readonly NpcDefinition[] Npcs = [
        new("fargoth", "Fargoth", "Wood Elf", "Male", 10f, 5f, "Commoner", Level: 2, Gold: 25,
            Equipped: ["Common Shirt", "Common Pants", "Common Shoes"],
            Items: [new("p_restore_health_s", "Potion of Restore Health", 2)]),
        new("arrille", "Arrille", "High Elf", "Male", 50f, 30f, "Trader Service", Level: 6, Gold: 800,
            Items: [new("iron_dagger", "Iron Dagger", 3), new("ingred_scamp_skin_01", "Scamp Skin", 2)]),
        new("sellus_gravius", "Sellus Gravius", "Imperial", "Male", 5f, 2f, "Guard", "Imperial Legion", "Knight Errant", 10, 120,
            Equipped: ["Imperial Steel Cuirass", "Imperial Broadsword"]),
        new("hrisskar_flat-foot", "Hrisskar Flat-Foot", "Nord", "Male", 55f, 32f, "Warrior", Level: 8, Gold: 60),
    ];

    public static NpcDefinition? FindNpc(string npcId) =>
        Npcs.FirstOrDefault(n => string.Equals(n.ObjectId, npcId, StringComparison.OrdinalIgnoreCase));

    public static GetNpcInfoResponsePayload ToNpcInfo(NpcDefinition npc) =>
        new(npc.ObjectId, npc.Name, npc.Race, npc.Sex, npc.ClassName, npc.Faction, npc.FactionRank);

    public static GetSceneContextResponsePayload GetSceneContext(string npcId) {
        var npc = FindNpc(npcId);
        var npcState = npc == null ? null : new SceneNpcState(npc.Level, 1f, npc.Gold, npc.Equipped ?? [], npc.Items ?? []);
        var player = new ScenePlayerState("Nerevar", "Dark Elf", "male", "Battlemage", 3, 0.7f, 0.9f, ["Chitin Cuirass", "Iron Longsword"]);
        return new(GameTime, CellName, npcState, player);
    }

    public static GetPlayerInfoResponsePayload PlayerInfo =>
        new(PlayerId, "Player", "Dark Elf", "Male");

    public static NearbyCharacterInfo[] GetCharactersWhoHear(string speakerId, float? maxDistance) {
        var speaker = FindNpc(speakerId);
        float sx = speaker?.X ?? 0f;
        float sy = speaker?.Y ?? 0f;

        var result = new List<NearbyCharacterInfo>();
        foreach (var npc in Npcs) {
            if (string.Equals(npc.ObjectId, speakerId, StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var dist = MathF.Sqrt((npc.X - sx) * (npc.X - sx) + (npc.Y - sy) * (npc.Y - sy));
            if (maxDistance.HasValue && dist > maxDistance.Value) {
                continue;
            }

            result.Add(new NearbyCharacterInfo(npc.ObjectId, dist));
        }

        return result.ToArray();
    }
}
