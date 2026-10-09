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
        var npcState = npc == null ? null : new SceneNpcState(npc.Level, 1f, Inventories[npc.ObjectId].GetValueOrDefault(GoldId), npc.Equipped ?? [], npc.Items ?? []);
        var player = new ScenePlayerState("Nerevar", "Dark Elf", "male", "Battlemage", 3, 0.7f, 0.9f, ["Chitin Cuirass", "Iron Longsword"]);
        return new(GameTime, CellName, npcState, player);
    }

    private const string GoldId = "gold_001";

    // Item counts by character, so transfers can succeed or fail like in the game
    private static readonly Dictionary<string, Dictionary<string, int>> Inventories = BuildInventories();

    private static Dictionary<string, Dictionary<string, int>> BuildInventories() {
        var inventories = new Dictionary<string, Dictionary<string, int>>(StringComparer.OrdinalIgnoreCase) {
            [PlayerId] = new(StringComparer.OrdinalIgnoreCase) { [GoldId] = 100 },
        };
        foreach (var npc in Npcs) {
            var items = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { [GoldId] = npc.Gold };
            foreach (var item in npc.Items ?? []) {
                items[item.Id] = item.Count;
            }
            inventories[npc.ObjectId] = items;
        }
        return inventories;
    }

    public static ActionResultPayload TransferItem(TransferItemPayload transfer) {
        if (!Inventories.TryGetValue(transfer.FromCharacterId, out var from) || !Inventories.TryGetValue(transfer.ToCharacterId, out var to)) {
            return new(false, "character not found");
        }

        var have = from.GetValueOrDefault(transfer.ItemId);
        if (have < transfer.Count) {
            return new(false, have == 0 ? "does not have it" : $"has only {have}");
        }

        from[transfer.ItemId] = have - transfer.Count;
        to[transfer.ItemId] = to.GetValueOrDefault(transfer.ItemId) + transfer.Count;
        var name = Npcs.SelectMany(n => n.Items ?? []).FirstOrDefault(i => i.Id == transfer.ItemId)?.Name
            ?? (transfer.ItemId == GoldId ? "Gold" : transfer.ItemId);
        return new(true, null, name);
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
