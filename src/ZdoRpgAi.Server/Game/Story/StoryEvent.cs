using System.Text.Json.Serialization;

namespace ZdoRpgAi.Server.Game.Story;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(PlayerSpeak), "PlayerSpeak")]
[JsonDerivedType(typeof(NpcSpeak), "NpcSpeak")]
[JsonDerivedType(typeof(NpcAction), "NpcAction")]
[JsonDerivedType(typeof(PlayerApproach), "PlayerApproach")]
public abstract record StoryEvent {
    public long Id { get; init; } = -1;
    public required string GameTime { get; init; }
    public string RealTime { get; init; } = "";
    // Cell where it happened, null for events recorded before cells were tracked
    public string? Cell { get; init; }
    // The game clock in seconds, null for events recorded before it was kept
    public double? GameSeconds { get; init; }

    public static T Create<T>(T evt) where T : StoryEvent =>
        evt with { RealTime = GetRealTime() };

    internal static string GetRealTime() =>
        DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    public static string DescribeAction(NpcAction action) =>
        action.Error == null ? $"[action] {action.Description}" : $"[action failed: {action.Error}] {action.Description}";

    public sealed record PlayerSpeak : StoryEvent {
        public required string PlayerCharacterId { get; init; }
        public string? TargetCharacterId { get; init; }
        public required string Text { get; init; }
    }

    public sealed record NpcSpeak : StoryEvent {
        public required string NpcCharacterId { get; init; }
        public string? TargetCharacterId { get; init; }
        public required string Text { get; init; }
    }

    // The player walked up to an NPC that remembers them, which prompts a greeting
    public sealed record PlayerApproach : StoryEvent {
        public required string PlayerCharacterId { get; init; }
        public required string NpcCharacterId { get; init; }
    }

    // A game action the NPC took, e.g. "gives the player 50 gold". Error is set if the game refused it.
    // Tool: the one the NPC called, like give_item; null in events recorded before it was kept.
    public sealed record NpcAction : StoryEvent {
        public required string NpcCharacterId { get; init; }
        public required string Description { get; init; }
        public string? Error { get; init; }
        public string? Tool { get; init; }
    }
}
