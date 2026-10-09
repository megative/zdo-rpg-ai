using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Server.Game.Story;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.Server.Game.Director;

// Game actions an NPC can take while talking, offered to the LLM as tools. The mod checks each one
// against the game state, so an NPC can't hand out items nobody has. Item transfers follow
// waterbobo24's transfer_item (github.com/waterbobo24/Morrowind-llm-server-client).
public class NpcActions {
    private static readonly ILog Log = Logger.Get<NpcActions>();

    private const string GoldId = "gold_001";
    private const int MaxDispositionChange = 25;
    public static readonly string[] Services = ["barter", "training", "travel", "spells", "repair"];

    private static readonly List<LlmToolParameter> ItemParameters = [
        new() { Name = "item_id", Description = $"Item id, as listed in your belongings; gold is \"{GoldId}\"" },
        new() { Name = "count", Type = "integer", Description = "How many, e.g. the exact amount of gold agreed on" },
    ];

    public static readonly List<LlmTool> Tools = [
        new() {
            Name = "give_item",
            Description = "Give the player an item or gold you carry: a gift, a sale, a reward.",
            Parameters = ItemParameters,
        },
        new() {
            Name = "take_item",
            Description = "Take an item or gold the player hands you: a payment, a bribe, a gift. The game checks they really have it.",
            Parameters = ItemParameters,
        },
        new() { Name = "follow_player", Description = "Start following the player, e.g. when you agree to accompany or escort them." },
        new() { Name = "stop_following", Description = "Stop following the player." },
        new() { Name = "attack_player", Description = "Attack the player. Only when they truly provoke or threaten you." },
        new() { Name = "stop_attacking", Description = "Stop attacking the player and calm down." },
        new() {
            Name = "change_disposition",
            Description = "Change how much you like the player when they offend, flatter, help or cheat you. It changes your prices and willingness to help.",
            Parameters = [
                new() { Name = "delta", Type = "integer", Description = $"From -{MaxDispositionChange} (deeply offended) to {MaxDispositionChange} (won over); small talk is -5..5" },
                new() { Name = "reason", Description = "A few words on why" },
            ],
        },
        new() {
            Name = "open_service",
            Description = "Open your trade, training, travel, spell or repair window when the player asks for a service you offer.",
            Parameters = [
                new() { Name = "service", Description = "Which service", EnumValues = [.. Services] },
            ],
        },
    ];

    private readonly IRpcChannel _rpc;

    public NpcActions(IRpcChannel rpc) {
        _rpc = rpc;
    }

    // Runs the tool calls in order. Each becomes a story event, so the NPC remembers what it did
    // and whether the game let it happen.
    public async Task<List<StoryEvent>> ExecuteAsync(string npcId, string playerId, string gameTime, string? cell, IReadOnlyList<LlmToolCall> calls) {
        var events = new List<StoryEvent>();
        foreach (var call in calls) {
            var (description, result) = await ExecuteAsync(npcId, playerId, call);
            var error = result.Success ? null : result.Error ?? "failed";
            Log.Info("NPC {NpcId} {Action}: {Result}", npcId, description, error ?? "done");
            events.Add(StoryEvent.Create(new StoryEvent.NpcAction {
                NpcCharacterId = npcId,
                GameTime = gameTime,
                Cell = cell,
                Description = description,
                Error = error,
            }));
        }
        return events;
    }

    private async Task<(string Description, ActionResultPayload Result)> ExecuteAsync(string npcId, string playerId, LlmToolCall call) {
        switch (call.Name) {
            case "give_item":
            case "take_item": {
                    var giving = call.Name == "give_item";
                    var itemId = GetString(call.Arguments, "item_id");
                    var count = GetInt(call.Arguments, "count") ?? 1;
                    if (itemId == null || count < 1) {
                        var what = giving ? "gives the player something" : "takes something from the player";
                        return (what, new ActionResultPayload(false, "no item or count given"));
                    }

                    var result = await CallAsync(nameof(ServerToModMessageType.TransferItem),
                        new TransferItemPayload(giving ? npcId : playerId, giving ? playerId : npcId, itemId, count),
                        PayloadJsonContext.Default.TransferItemPayload);
                    var item = DescribeItem(itemId, result.ItemName, count);
                    return (giving ? $"gives the player {item}" : $"takes {item} from the player", result);
                }
            case "follow_player":
                return ("starts following the player", await CallAsync(nameof(ServerToModMessageType.NpcStartFollowCharacter),
                    new NpcStartFollowCharacterPayload(npcId, playerId), PayloadJsonContext.Default.NpcStartFollowCharacterPayload));
            case "stop_following":
                return ("stops following the player", await CallAsync(nameof(ServerToModMessageType.NpcStopFollowCharacter),
                    new NpcStopFollowCharacterPayload(npcId), PayloadJsonContext.Default.NpcStopFollowCharacterPayload));
            case "attack_player":
                return ("attacks the player", await CallAsync(nameof(ServerToModMessageType.NpcAttack),
                    new NpcAttackPayload(npcId, playerId), PayloadJsonContext.Default.NpcAttackPayload));
            case "stop_attacking":
                return ("stops attacking the player", await CallAsync(nameof(ServerToModMessageType.NpcStopAttack),
                    new NpcStopAttackPayload(npcId), PayloadJsonContext.Default.NpcStopAttackPayload));
            case "change_disposition": {
                    var delta = Math.Clamp(GetInt(call.Arguments, "delta") ?? 0, -MaxDispositionChange, MaxDispositionChange);
                    var reason = GetString(call.Arguments, "reason");
                    var description = $"{(delta >= 0 ? "warms to" : "cools toward")} the player ({delta:+0;-0;0}{(reason == null ? "" : $", {reason}")})";
                    if (delta == 0) {
                        return (description, new ActionResultPayload(true));
                    }
                    return (description, await CallAsync(nameof(ServerToModMessageType.ModifyDisposition),
                        new ModifyDispositionPayload(npcId, playerId, delta), PayloadJsonContext.Default.ModifyDispositionPayload));
                }
            case "open_service": {
                    var service = GetString(call.Arguments, "service")?.ToLowerInvariant();
                    if (service == null || !Services.Contains(service)) {
                        return ("offers a service", new ActionResultPayload(false, "unknown service"));
                    }
                    return ($"opens the {service} window for the player", await CallAsync(nameof(ServerToModMessageType.OpenService),
                        new OpenServicePayload(npcId, service), PayloadJsonContext.Default.OpenServicePayload));
                }
            default:
                Log.Warn("LLM called unknown tool {Tool}", call.Name);
                return ($"tries to {call.Name}", new ActionResultPayload(false, "no such action"));
        }
    }

    private async Task<ActionResultPayload> CallAsync<T>(string type, T payload, JsonTypeInfo<T> typeInfo) {
        try {
            var response = await _rpc.CallAsync(type, JsonExtensions.SerializeToObject(payload, typeInfo));
            return response.Json?.DeserializeSafe(PayloadJsonContext.Default.ActionResultPayload)
                ?? new ActionResultPayload(false, "the game gave no answer");
        }
        catch (Exception ex) {
            Log.Warn("Action {Type} failed: {Error}", type, ex.Message);
            return new ActionResultPayload(false, "the game gave no answer");
        }
    }

    private static string DescribeItem(string itemId, string? name, int count) {
        if (string.Equals(itemId, GoldId, StringComparison.OrdinalIgnoreCase)) {
            return $"{count} gold";
        }
        var label = name ?? itemId;
        return count > 1 ? $"{label} x{count}" : label;
    }

    private static string? GetString(JsonObject args, string key) =>
        args[key] is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text) ? text : null;

    // Models send counts as 50, 50.0 or "50"
    private static int? GetInt(JsonObject args, string key) {
        if (args[key] is not JsonValue value) {
            return null;
        }
        if (value.TryGetValue<int>(out var number)) {
            return number;
        }
        if (value.TryGetValue<double>(out var real)) {
            return (int)Math.Round(real);
        }
        return value.TryGetValue<string>(out var text) && int.TryParse(text, out var parsed) ? parsed : null;
    }
}
