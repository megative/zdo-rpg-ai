using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Game.Npc;
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
    // A quest that moved on to its next step; never a stored status
    private const string Updated = "Updated";
    public static readonly string[] Services = ["barter", "training", "travel", "spells", "repair"];

    private static readonly List<LlmToolParameter> ItemParameters = [
        new() { Name = "item_id", Description = $"Item id, as listed in your belongings; gold is \"{GoldId}\"" },
        new() { Name = "count", Type = "integer", Description = "How many, e.g. the exact amount of gold agreed on" },
    ];

    private static readonly List<LlmTool> BaseTools = [
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
                new() { Name = "reason", Description = "A few words on why, in the language you speak: the player sees them" },
            ],
        },
        new() {
            Name = "give_task",
            Description = "Ask the player to do something concrete for you: fetch an item, deliver a message, deal with someone. Only promise a reward you can pay.",
            Parameters = [
                new() { Name = "description", Description = "The task in a few words, as the player should remember it" },
                new() { Name = "reward", Description = "What you promise in return, e.g. \"50 gold\", in the language you speak", Required = false },
            ],
        },
        new() {
            Name = "complete_task",
            Description = "Mark a task you gave as done once the player really did it. Take the items they bring with take_item first, then pay the reward with give_item.",
            Parameters = [new() { Name = "task_id", Type = "integer", Description = "Number of the task" }],
        },
        new() {
            Name = "cancel_task",
            Description = "Call off a task you gave, e.g. when it no longer matters or the player refuses it.",
            Parameters = [new() { Name = "task_id", Type = "integer", Description = "Number of the task" }],
        },
        new() {
            Name = "open_dialogue",
            Description = "Open the game's own dialogue with you, after your words. Use it when the player needs what only that dialogue gives: "
                + "quest orders and quest items, reporting on a quest, joining or rising in a faction, or what to do next in their quest.",
        },
        new() {
            Name = "open_service",
            Description = "Open your trade, training, travel, spell or repair window when the player asks for a service you offer.",
            Parameters = [
                new() { Name = "service", Description = "Which service", EnumValues = [.. Services] },
            ],
        },
    ];

    // Alpha, with director.questChains on
    private static readonly List<LlmTool> QuestTools = [
        new() {
            Name = "start_quest",
            Description = "Give the player a small quest of one or two steps, each a visit to someone from PEOPLE THE PLAYER HAS MET; "
                + "at the end they come back to you for the reward. Only when it fits who you are and what you talk about.",
            Parameters = [
                new() { Name = "title", Description = "A short name for the quest, in the language you speak" },
                new() { Name = "step1_npc", Description = "Id of whom the player goes to first, from PEOPLE THE PLAYER HAS MET" },
                new() { Name = "step1_goal", Description = "What the player does there, naming the person and the place, in the language you speak" },
                new() { Name = "step2_npc", Description = "Id of whom they go to next, for a second step", Required = false },
                new() { Name = "step2_goal", Description = "What they do there", Required = false },
                new() { Name = "final_goal", Description = "What they bring or tell you at the end, in the language you speak", Required = false },
                new() { Name = "reward", Description = "What you promise in return, in the language you speak", Required = false },
            ],
        },
        new() {
            Name = "advance_quest",
            Description = "Mark the player's step of a quest done, when the step is with you and they really did what it asks.",
            Parameters = [new() { Name = "quest_id", Type = "integer", Description = "Number of the quest" }],
        },
    ];

    private readonly IRpcChannel _rpc;
    private readonly ISaveGameRepository _saveGameRepo;
    private readonly NpcRepository _npcRepo;
    private readonly DirectorSection _config;

    public NpcActions(IRpcChannel rpc, ISaveGameRepository saveGameRepo, NpcRepository npcRepo, DirectorSection config) {
        _rpc = rpc;
        _saveGameRepo = saveGameRepo;
        _npcRepo = npcRepo;
        _config = config;
    }

    public List<LlmTool> Tools => _config.QuestChains ? [.. BaseTools, .. QuestTools] : BaseTools;

    public List<RawNpcTask> GetOpenTasks(string npcId) => _saveGameRepo.GetOpenNpcTasks(npcId);

    // Runs the tool calls in order. Each becomes a story event, so the NPC remembers what it did
    // and whether the game let it happen.
    public async Task<List<StoryEvent.NpcAction>> ExecuteAsync(string npcId, string playerId, string gameTime, double? gameSeconds, string? cell, IReadOnlyList<LlmToolCall> calls) {
        var events = new List<StoryEvent.NpcAction>();
        var anyFailed = false;
        foreach (var call in calls) {
            // Completing a task right after, say, take_item failed would reward work not done
            var (description, result) = call.Name == "complete_task" && anyFailed
                ? ("marks a task as done", new ActionResultPayload(false, "an earlier action failed"))
                : await ExecuteAsync(npcId, playerId, gameTime, gameSeconds, call);
            anyFailed |= !result.Success;
            if (result.Success) {
                TrackCompanion(npcId, call.Name, gameTime, gameSeconds);
            }
            var error = result.Success ? null : result.Error ?? "failed";
            Log.Info("NPC {NpcId} {Action}: {Result}", npcId, description, error ?? "done");
            events.Add(StoryEvent.Create(new StoryEvent.NpcAction {
                NpcCharacterId = npcId,
                GameTime = gameTime,
                GameSeconds = gameSeconds,
                Cell = cell,
                Description = description,
                Error = error,
                Tool = call.Name,
            }));
        }
        return events;
    }

    // Following the player makes a companion; leaving them or turning on them ends it
    private void TrackCompanion(string npcId, string tool, string gameTime, double? gameSeconds) {
        switch (tool) {
            case "follow_player":
                _saveGameRepo.AddCompanion(npcId, gameTime, gameSeconds);
                break;
            case "stop_following":
            case "attack_player":
                _saveGameRepo.RemoveCompanion(npcId);
                break;
        }
    }

    private async Task<(string Description, ActionResultPayload Result)> ExecuteAsync(string npcId, string playerId, string gameTime, double? gameSeconds, LlmToolCall call) {
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
                        new ModifyDispositionPayload(npcId, playerId, delta, reason), PayloadJsonContext.Default.ModifyDispositionPayload));
                }
            case "give_task": {
                    var task = GetString(call.Arguments, "description");
                    if (task == null) {
                        return ("gives the player a task", new ActionResultPayload(false, "no description"));
                    }
                    var reward = GetString(call.Arguments, "reward");
                    var taskId = _saveGameRepo.AddNpcTask(npcId, task, reward, gameTime, gameSeconds);
                    PublishTaskUpdate(npcId, taskId, task, reward, nameof(NpcTaskStatus.Open));
                    return ($"gives the player task #{taskId}: {task}{(reward == null ? "" : $" (reward: {reward})")}", new ActionResultPayload(true));
                }
            case "complete_task":
            case "cancel_task": {
                    var done = call.Name == "complete_task";
                    var verb = done ? "marks as done" : "calls off";
                    var taskId = GetInt(call.Arguments, "task_id");
                    var task = _saveGameRepo.GetOpenNpcTasks(npcId).FirstOrDefault(t => t.Id == taskId);
                    var status = done ? NpcTaskStatus.Done : NpcTaskStatus.Cancelled;
                    // A quest is done once the player has been everywhere it sent them
                    var pending = task == null ? null : _saveGameRepo.GetQuestSteps(task.Id).FirstOrDefault(s => !s.Done && s.NpcId != npcId);
                    if (done && pending != null) {
                        return ($"{verb} quest #{task!.Id}", new ActionResultPayload(false, $"the player still has to {pending.Goal}"));
                    }
                    if (task == null || !_saveGameRepo.CloseNpcTask(task.Id, npcId, status, gameTime, gameSeconds)) {
                        return ($"{verb} task #{taskId}", new ActionResultPayload(false, "no such open task"));
                    }
                    PublishTaskUpdate(npcId, task.Id, task.Description, task.Reward, status.ToString());
                    return ($"{verb} task #{task.Id}: {task.Description}", new ActionResultPayload(true));
                }
            case "start_quest":
                return StartQuest(npcId, call.Arguments, gameTime, gameSeconds);
            case "advance_quest":
                return AdvanceQuest(npcId, call.Arguments, gameSeconds);
            case "open_dialogue":
                return ("opens the dialogue with the player", await CallAsync(nameof(ServerToModMessageType.OpenDialogue),
                    new OpenDialoguePayload(npcId), PayloadJsonContext.Default.OpenDialoguePayload));
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

    // A task whose steps send the player to people they have met, the last one back to the giver.
    // The player's log shows the title and what to do next.
    private (string Description, ActionResultPayload Result) StartQuest(string npcId, JsonObject args, string gameTime, double? gameSeconds) {
        if (!_config.QuestChains) {
            return ("starts a quest", new ActionResultPayload(false, "quests are off"));
        }
        var title = GetString(args, "title");
        var steps = new[] { (Npc: GetString(args, "step1_npc"), Goal: GetString(args, "step1_goal")), (Npc: GetString(args, "step2_npc"), Goal: GetString(args, "step2_goal")) }
            .Where(s => s.Npc != null || s.Goal != null)
            .ToList();
        if (title == null || steps.Count == 0 || steps.Any(s => s.Npc == null || s.Goal == null)) {
            return ("starts a quest", new ActionResultPayload(false, "a quest needs a title and, for each step, someone to see and what to do"));
        }

        var met = _npcRepo.GetMet().Select(n => n.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var stranger = steps.FirstOrDefault(s => string.Equals(s.Npc, npcId, StringComparison.OrdinalIgnoreCase) || !met.Contains(s.Npc!));
        if (stranger.Npc != null) {
            return ($"starts the quest \"{title}\"", new ActionResultPayload(false, $"{stranger.Npc} is not someone else the player has met"));
        }

        var reward = GetString(args, "reward");
        var final = GetString(args, "final_goal") ?? title;
        var taskId = _saveGameRepo.AddNpcTask(npcId, title, reward, gameTime, gameSeconds);
        _saveGameRepo.AddQuestSteps(taskId, [.. steps.Select(s => (s.Npc!, s.Goal!)), (npcId, final)]);
        PublishTaskUpdate(npcId, taskId, $"{title}: {steps[0].Goal}", reward, nameof(NpcTaskStatus.Open));
        return ($"gives the player quest #{taskId} \"{title}\": {string.Join("; then ", steps.Select(s => s.Goal))}; then back to you: {final}"
            + (reward == null ? "" : $" (reward: {reward})"), new ActionResultPayload(true));
    }

    // The NPC of the player's current step says it's done; the log moves on to the next one
    private (string Description, ActionResultPayload Result) AdvanceQuest(string npcId, JsonObject args, double? gameSeconds) {
        var questId = GetInt(args, "quest_id");
        var quest = questId == null ? null : _saveGameRepo.GetOpenNpcTask(questId.Value);
        var steps = quest == null ? [] : _saveGameRepo.GetQuestSteps(quest.Id);
        var current = steps.FirstOrDefault(s => !s.Done);
        if (quest == null || current == null || current.Index == steps[^1].Index
            || !string.Equals(current.NpcId, npcId, StringComparison.OrdinalIgnoreCase)) {
            return ($"moves quest #{questId} on", new ActionResultPayload(false, "the player's step in it isn't with you"));
        }

        _saveGameRepo.MarkQuestStepDone(quest.Id, current.Index, gameSeconds);
        var next = steps.First(s => s.Index > current.Index);
        PublishTaskUpdate(quest.NpcId, quest.Id, $"{quest.Description}: {next.Goal}", quest.Reward, Updated);
        return ($"marks the player's step of quest #{quest.Id} done: {current.Goal}", new ActionResultPayload(true));
    }

    private void PublishTaskUpdate(string npcId, long taskId, string description, string? reward, string status) {
        _rpc.Publish(nameof(ServerToModMessageType.TaskUpdate), JsonExtensions.SerializeToObject(
            new TaskUpdatePayload(npcId, description, status, taskId, reward), PayloadJsonContext.Default.TaskUpdatePayload));
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
