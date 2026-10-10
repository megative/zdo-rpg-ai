using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ZdoRpgAi.Server.Bootstrap;

namespace ZdoRpgAi.ModEmulator.Scenarios;

// A scenario file: a few steps of play in Seyda Neen, each with what should come of it. Played
// against the model from the server's config, so it shows how that model handles the game.
public class Scenario {
    public string Name { get; set; } = "";
    public string? About { get; set; }
    // Director settings to change, named as in the server config; the rest come from the config
    public JsonObject? Director { get; set; }
    public WorldChanges? World { get; set; }
    public List<ScenarioStep> Steps { get; set; } = [];
}

// Differences from the built-in world
public class WorldChanges {
    // Replace the player's factions, e.g. to make them one of the Blades
    public List<FactionChange>? PlayerFactions { get; set; }
    // Replace the player's journal
    public List<QuestChange>? Quests { get; set; }
    public string? Time { get; set; }
}

public class FactionChange {
    public string Name { get; set; } = "";
    public string? Rank { get; set; }
}

public class QuestChange {
    public string Name { get; set; } = "";
    public string Entry { get; set; } = "";
    // The NPCs taking part, the only ones told of the quest
    public List<string> Npcs { get; set; } = [];
}

// One thing the player does: look at an NPC ("none" looks away), let game hours pass, say a line,
// go to a cell, let two NPCs nearby talk, let a companion speak up, or wait some real seconds
public class ScenarioStep {
    public string? Target { get; set; }
    public double? Hours { get; set; }
    public string? Say { get; set; }
    public string? Go { get; set; }
    public bool Banter { get; set; }
    public bool Companion { get; set; }
    public double? Wait { get; set; }
    public List<Expectation> Expect { get; set; } = [];
}

// What should follow a step: set one of these per item
public class Expectation {
    // That NPC said something; or said nothing
    public string? Replies { get; set; }
    public string? Silent { get; set; }
    // Every NPC line is in this language
    public string? Language { get; set; }
    // A line has, or no line has, this text (any NPC, unless one is named)
    public TextCheck? Mentions { get; set; }
    public TextCheck? Avoids { get; set; }
    // The NPC did, or did not do, this: gave, took, disposition, follow, stopFollow, attack,
    // stopAttack, taskGiven, taskUpdated, taskDone, taskCancelled, dialogue, service
    public ActionCheck? Action { get; set; }
    public ActionCheck? NoAction { get; set; }
    // The instructions the NPC got for its reply have, or lack, this text
    public TextCheck? PromptHas { get; set; }
    public TextCheck? PromptLacks { get; set; }
    // A criterion in plain words that the model checks the conversation against
    public string? Judge { get; set; }
}

public class TextCheck {
    public string? Npc { get; set; }
    public string Text { get; set; } = "";
}

public class ActionCheck {
    public string Npc { get; set; } = "";
    public string Kind { get; set; } = "";
    // For disposition: + or -
    public string? Sign { get; set; }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    UseStringEnumConverter = true,
    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(Scenario))]
[JsonSerializable(typeof(ServerConfig))]
[JsonSerializable(typeof(DirectorSection))]
internal partial class ScenarioJsonContext : JsonSerializerContext;
