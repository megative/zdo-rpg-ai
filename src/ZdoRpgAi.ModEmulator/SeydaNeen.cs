using ZdoRpgAi.Protocol.Messages;

namespace ZdoRpgAi.ModEmulator;

// The village the game starts in, a few of its people, and Caius Cosades in Balmora, where the
// player is sent first
public static class SeydaNeen {
    public const string Cell = "Seyda Neen";
    public const string Balmora = "Balmora, Caius Cosades' House";

    public static WorldDefinition Create() => new(
        Cell: Cell,
        GameTime: "09:30, 16 Last Seed 3E 427",
        Player: new PlayerDefinition("Nerevar", "Dark Elf", "male", "Battlemage", 3, ["Chitin Cuirass", "Iron Longsword"],
            [new("Fighters Guild", "Journeyman")], HealthRatio: 0.7f, FatigueRatio: 0.9f),
        Npcs: [
            new("fargoth", "Fargoth", "Wood Elf", "Male", 10f, 5f, "Commoner", Level: 2, Gold: 25,
                Equipped: ["Common Shirt", "Common Pants", "Common Shoes"],
                Items: [new("p_restore_health_s", "Potion of Restore Health", 2)],
                QuestTopics: [new("ring", null, Starts: true, Finishes: false,
                    "I swear one of the guards has it. My engraved healing ring, a family heirloom. You haven't seen it, have you?",
                    "I met a Bosmer named Fargoth who claims Imperial guards stole his ring. He'd like it back.")]),
            new("arrille", "Arrille", "High Elf", "Male", 50f, 30f, "Trader Service", Level: 6, Gold: 800,
                Items: [new("iron_dagger", "Iron Dagger", 3), new("ingred_scamp_skin_01", "Scamp Skin", 2)],
                Services: ["barter"]),
            new("sellus_gravius", "Sellus Gravius", "Imperial", "Male", 5f, 2f, "Guard", "Imperial Legion", "Knight Errant", 10, 120,
                Equipped: ["Imperial Steel Cuirass", "Imperial Broadsword"]),
            new("hrisskar_flat-foot", "Hrisskar Flat-Foot", "Nord", "Male", 55f, 32f, "Warrior", Level: 8, Gold: 60),
            new("caius_cosades", "Caius Cosades", "Imperial", "Male", 2f, 2f, "Monk", "Blades", Level: 20, Gold: 200,
                Cell: Balmora),
        ],
        Quests: [
            new("Report to Caius Cosades", "I've been told to go to Balmora and report to a man named Caius Cosades.",
                ["sellus_gravius", "caius_cosades"]),
        ],
        Topics: new() {
            ["Seyda Neen"] = ["A small port village on the Bitter Coast. The Census and Excise Office is by the docks."],
            ["latest rumors"] = ["They say a smuggler's den is hidden somewhere along the Bitter Coast."],
            ["Balmora"] = ["Balmora is to the north-east. Take the silt strider, it's quicker than walking."],
        },
        PlayerItems: new() { [EmulatedWorld.GoldId] = 100, ["ingred_scamp_skin_01"] = 2 });
}
