using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Game.Npc;
using ZdoRpgAi.Server.Game.Story;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.Server.Game.Director;

public class SimpleReactiveStrategy : IDirectorStrategy {
    private static readonly ILog Log = Logger.Get<SimpleReactiveStrategy>();

    private const int LoreWaitMs = 2500;

    private readonly ILlm _mainLlm;
    private readonly ILlm _simpleLlm;
    private readonly IStory _story;
    private readonly NpcRepository _npcRepo;
    private readonly IRpcChannel _rpc;
    private readonly DirectorHelper _directorHelper;
    private readonly DirectorSection _config;
    private readonly NpcActions _actions;
    private readonly NpcPersonalityWriter _personalityWriter;
    private readonly RumorMill _rumors;
    private readonly LoreLibrary _lore;

    public SimpleReactiveStrategy(
        ILlm mainLlm, ILlm simpleLlm, IStory story, NpcRepository npcRepo, IRpcChannel rpc,
        DirectorHelper directorHelper, DirectorSection config, RumorMill rumors, ISaveGameRepository saveGameRepo) {
        _mainLlm = mainLlm;
        _simpleLlm = simpleLlm;
        _story = story;
        _npcRepo = npcRepo;
        _rpc = rpc;
        _directorHelper = directorHelper;
        _config = config;
        _actions = new NpcActions(rpc, saveGameRepo);
        _personalityWriter = new NpcPersonalityWriter(simpleLlm);
        _rumors = rumors;
        _lore = new LoreLibrary(rpc);
    }

    public async Task<List<StoryEvent>> ProcessStoryEventsAsync(List<StoryEvent> events) {
        Log.Trace("Processing {Count} events", events.Count);

        var playerIds = events.OfType<StoryEvent.PlayerSpeak>().Select(ps => ps.PlayerCharacterId)
            .Concat(events.OfType<StoryEvent.PlayerApproach>().Select(pa => pa.PlayerCharacterId))
            .ToHashSet();
        Log.Trace("Found {Count} player IDs: {Ids}", playerIds.Count, string.Join(", ", playerIds));

        var (npcId, gameTime) = await FindLastTargetedNpcAsync(_rpc, events, playerIds);
        if (npcId == null) {
            Log.Debug("No target NPC found in events");
            return [];
        }

        Log.Trace("Target NPC: {NpcId}, game time: {GameTime}", npcId, gameTime);
        var playerId = playerIds.FirstOrDefault();
        _directorHelper.PublishNpcThinking(npcId);

        try {
            // Ask the game for the current scene, and for its own dialogue on whatever the player
            // mentioned, while NPC info and history load
            var sceneTask = playerId != null
                ? _directorHelper.QuerySceneContextAsync(npcId, playerId)
                : Task.FromResult<GetSceneContextResponsePayload?>(null);
            var playerText = events.OfType<StoryEvent.PlayerSpeak>().LastOrDefault()?.Text;
            var loreTask = playerText != null
                ? _lore.RecallAsync(npcId, playerText)
                : Task.FromResult<List<TopicLines>>([]);

            var npcInfo = await _npcRepo.GetNpcInfoAsync(npcId);
            if (npcInfo == null) {
                Log.Warn("Could not get info for NPC {NpcId}", npcId);
                return [];
            }

            // First talk with this NPC: sketch its character while history and scene load
            var personalityTask = npcInfo.Personality == null
                ? WritePersonalityAsync(npcInfo, sceneTask)
                : Task.FromResult<string?>(npcInfo.Personality);

            Log.Trace("NPC info: {Name} ({Race} {Sex})", npcInfo.Name, npcInfo.Race, npcInfo.Sex);
            var (history, summaries) = await _story.GetHistoryForCharacterAsync(npcId);
            Log.Trace("History: {HistoryCount} events, {SummaryCount} summaries", history.Count, summaries.Count);
            var scene = await sceneTask;
            var personality = await personalityTask;
            if (personality != null && npcInfo.Personality == null) {
                _npcRepo.SavePersonality(npcInfo, personality);
                npcInfo = npcInfo with { Personality = personality };
            }
            var rumors = await _rumors.GetRumorsAsync(npcId, scene?.CellName);
            // Lore is a bonus: the reply doesn't wait long for it (the topic list loads in the background)
            var lore = await Task.WhenAny(loreTask, Task.Delay(LoreWaitMs)) == loreTask ? loreTask.Result : [];
            var request = await BuildNpcRequestAsync(npcInfo, scene, rumors, lore, history, summaries);
            var response = await _mainLlm.ChatAsync(request);
            Log.Trace("Main LLM response: {Length} chars, {ToolCount} tool calls", response.Text?.Length ?? 0, response.ToolCalls?.Count ?? 0);
            var text = response.Text?.Trim();
            var calls = response.ToolCalls ?? [];
            if (string.IsNullOrEmpty(text) && calls.Count == 0) {
                Log.Warn("LLM returned no response for NPC {NpcId}", npcId);
                return [];
            }

            var eventTime = scene?.GameTime ?? gameTime!;
            var actions = calls.Count > 0 && playerId != null
                ? await _actions.ExecuteAsync(npcId, playerId, eventTime, scene?.CellName, calls)
                : [];
            if (string.IsNullOrEmpty(text) && actions.Count > 0) {
                // The model acted without a word, waiting to hear how it went: tell it, so the NPC speaks
                text = (await SpeakAfterActionsAsync(request, response, actions))?.Trim();
            }

            var newEvents = new List<StoryEvent>();
            if (!string.IsNullOrEmpty(text)) {
                Log.Trace("Generated response for NPC {NpcId}: {ResponseLength} chars", npcId, text.Length);
                newEvents.Add(StoryEvent.Create(new StoryEvent.NpcSpeak {
                    NpcCharacterId = npcId,
                    TargetCharacterId = playerId,
                    GameTime = eventTime,
                    Cell = scene?.CellName,
                    Text = text,
                }));
            }
            newEvents.AddRange(actions);
            return newEvents;
        }
        catch (Exception ex) {
            Log.Error("Failed to generate NPC response: {Error}", ex.Message);
            return [];
        }
    }

    private async Task<string?> WritePersonalityAsync(NpcInfo npc, Task<GetSceneContextResponsePayload?> sceneTask) {
        var scene = await sceneTask;
        var personality = await _personalityWriter.WriteAsync(npc, scene?.CellName);
        if (personality != null) {
            Log.Info("Wrote personality for {NpcId}: {Personality}", npc.Id, personality);
        }
        return personality;
    }

    private async Task<(string? NpcId, string? GameTime)> FindLastTargetedNpcAsync(
        IRpcChannel rpc, List<StoryEvent> events, HashSet<string> playerIds) {
        Log.Trace("Finding last targeted NPC from {Count} events", events.Count);
        for (var i = events.Count - 1; i >= 0; i--) {
            switch (events[i]) {
                case StoryEvent.PlayerSpeak ps:
                    Log.Trace("Checking PlayerSpeak event, explicit target: {Target}", ps.TargetCharacterId ?? "none");
                    var npcId = ps.TargetCharacterId ?? await DetermineTargetNpcAsync(rpc, ps);
                    if (npcId != null) {
                        return (npcId, ps.GameTime);
                    }

                    break;
                case StoryEvent.PlayerApproach pa:
                    return (pa.NpcCharacterId, pa.GameTime);
                case StoryEvent.NpcSpeak ns when ns.TargetCharacterId != null && !playerIds.Contains(ns.TargetCharacterId):
                    Log.Trace("Found NpcSpeak targeting non-player: {Target}", ns.TargetCharacterId);
                    return (ns.TargetCharacterId, ns.GameTime);
            }
        }
        return (null, null);
    }

    private async Task<string?> DetermineTargetNpcAsync(IRpcChannel rpc, StoryEvent.PlayerSpeak evt) {
        Log.Trace("Determining target NPC for player {PlayerId}", evt.PlayerCharacterId);
        var hearResponse = await rpc.CallAsync(
            nameof(ServerToModMessageType.GetCharactersWhoHear),
            JsonExtensions.SerializeToObject(
                new GetCharactersWhoHearRequestPayload(evt.PlayerCharacterId),
                PayloadJsonContext.Default.GetCharactersWhoHearRequestPayload));

        var payload = hearResponse.Json?.DeserializeSafe(PayloadJsonContext.Default.GetCharactersWhoHearResponsePayload);
        var nearby = payload?.Characters
            .Where(c => c.CharacterId != evt.PlayerCharacterId)
            .OrderBy(c => c.DistanceMeters)
            .ToArray() ?? [];

        Log.Trace("Found {Count} nearby characters", nearby.Length);
        if (nearby.Length == 0) {
            Log.Debug("No nearby NPCs to respond");
            return null;
        }

        if (nearby.Length == 1) {
            Log.Trace("Single nearby NPC: {NpcId}", nearby[0].CharacterId);
            return nearby[0].CharacterId;
        }

        var npcInfos = new List<(string Id, NpcInfo Info)>();
        foreach (var npc in nearby) {
            var info = await _npcRepo.GetNpcInfoAsync(npc.CharacterId);
            if (info != null) {
                npcInfos.Add((npc.CharacterId, info));
            }
        }

        Log.Trace("Resolved {Count} NPC infos out of {Total} nearby", npcInfos.Count, nearby.Length);
        if (npcInfos.Count == 0) {
            return null;
        }

        if (npcInfos.Count == 1) {
            Log.Trace("Single NPC with info: {NpcId}", npcInfos[0].Id);
            return npcInfos[0].Id;
        }

        var npcList = string.Join("\n", npcInfos.Select((n, i) =>
            $"- {n.Id}: {n.Info.Name} ({n.Info.Race} {n.Info.Sex}), distance: {nearby.First(c => c.CharacterId == n.Id).DistanceMeters:F1} meters"));

        Log.Trace("Asking simple LLM to choose among {Count} NPCs", npcInfos.Count);
        var request = new LlmRequest {
            SystemPrompt = "You are deciding which NPC a player is talking to. " +
                           "Respond with ONLY the character ID of the most likely target. " +
                           "Consider the speech content and NPC proximity. " +
                           "If unsure, pick the closest NPC.",
            Messages = [
                new LlmMessage {
                    Role = LlmRole.User,
                    Text = $"Nearby NPCs:\n{npcList}\n\nPlayer said: \"{evt.Text}\"\n\nWhich NPC ID is the player addressing?",
                },
            ],
        };

        var response = await _simpleLlm.ChatAsync(request);
        var chosenId = response.Text?.Trim();

        if (chosenId != null && npcInfos.Any(n => n.Id == chosenId)) {
            Log.Debug("Simple LLM chose NPC {NpcId} as target", chosenId);
            return chosenId;
        }

        Log.Debug("Simple LLM response '{Response}' did not match any NPC, falling back to closest", response.Text ?? "");
        return nearby[0].CharacterId;
    }

    private async Task<LlmRequest> BuildNpcRequestAsync(
        NpcInfo npc, GetSceneContextResponsePayload? scene, List<string> rumors, List<TopicLines> lore,
        List<StoryEvent> history, List<StoryEventSummary> summaries) {
        Log.Trace("Generating response for NPC {NpcName} with {HistoryCount} history events and {SummaryCount} summaries",
            npc.Name, history.Count, summaries.Count);

        var names = await ResolveNamesAsync(npc, history);
        var contextBlock = BuildContextBlock(summaries, history, names);
        var systemPrompt = BuildSystemPrompt(npc, scene, rumors, lore);

        var messages = new List<LlmMessage>();

        if (contextBlock != null) {
            messages.Add(new LlmMessage {
                Role = LlmRole.User,
                Text = contextBlock,
            });
            messages.Add(new LlmMessage {
                Role = LlmRole.Model,
                Text = "Understood, I have the conversation context.",
            });
        }

        if (history.Count > 0) {
            messages.Add(new LlmMessage {
                Role = LlmRole.User,
                Text = DescribeEvent(history[^1], names),
            });
        }

        var request = new LlmRequest {
            SystemPromptPrefix = BuildRules(),
            SystemPrompt = systemPrompt,
            Messages = messages,
            Tools = NpcActions.Tools,
        };

        Log.Trace("System prompt for {NpcId}:\n{SystemPrompt}", npc.Id, systemPrompt);
        Log.Trace("Calling main LLM with {MessageCount} messages", messages.Count);
        return request;
    }

    // Continues the conversation with the outcome of the actions and asks for words only. The
    // request is the first one plus two messages, so the cached prompt still applies.
    private async Task<string?> SpeakAfterActionsAsync(LlmRequest request, LlmResponse response, List<StoryEvent.NpcAction> actions) {
        var calls = response.ToolCalls!;
        var results = calls.Zip(actions, (call, action) => new LlmToolResult {
            CallId = call.Id,
            Name = call.Name,
            Result = action.Error == null ? "Done." : $"Failed: {action.Error}.",
        }).ToList();

        Log.Debug("NPC acted without words, asking for them after {Count} actions", results.Count);
        var followUp = await _mainLlm.ChatAsync(new LlmRequest {
            SystemPromptPrefix = request.SystemPromptPrefix,
            SystemPrompt = request.SystemPrompt,
            Messages = [
                .. request.Messages,
                new LlmMessage { Role = LlmRole.Model, Text = response.Text, ToolCalls = calls, ProviderContent = response.ProviderContent },
                new LlmMessage { Role = LlmRole.User, ToolResults = results },
            ],
            Tools = request.Tools,
            AllowToolCalls = false,
        });
        if (string.IsNullOrWhiteSpace(followUp.Text)) {
            Log.Warn("NPC stays silent: no words after its actions either");
        }
        return followUp.Text;
    }

    private string BuildSystemPrompt(NpcInfo npc, GetSceneContextResponsePayload? scene, List<string> rumors, List<TopicLines> lore) {
        var lines = new List<string> { $"You are {npc.Name}, {DescribeNpc(npc, scene?.Npc)}, living in Morrowind." };
        if (!string.IsNullOrEmpty(npc.Personality)) {
            lines.Add(npc.Personality);
        }

        if (!string.IsNullOrEmpty(scene?.GameTime)) {
            lines.Add($"It is {scene.GameTime}.");
        }
        if (!string.IsNullOrEmpty(scene?.CellName)) {
            lines.Add($"You are in {scene.CellName}.");
        }
        if (scene?.Npc is { } self) {
            if (DescribeHealth(self.HealthRatio) is { } health) {
                lines.Add($"You are {health}.");
            }
            if (self.Disposition is { } disposition) {
                lines.Add($"Your disposition toward the player is {disposition}/100 ({DescribeDisposition(disposition)}).");
            }
            if (self.Services is { Length: > 0 }) {
                lines.Add($"You offer these services: {string.Join(", ", self.Services)}.");
            }
            lines.Add($"You have {self.Gold} gold.");
            if (self.Equipped.Length > 0) {
                lines.Add($"You are wearing: {string.Join(", ", self.Equipped)}.");
            }
            if (self.Items.Length > 0) {
                var items = self.Items.Select(i => i.Count > 1 ? $"{i.Name} x{i.Count} (id {i.Id})" : $"{i.Name} (id {i.Id})");
                lines.Add($"You also carry: {string.Join(", ", items)}.");
            }
        }

        if (rumors.Count > 0) {
            lines.Add($"Rumors you've heard around {RumorMill.TownOf(scene?.CellName)}:");
            lines.AddRange(rumors.Select(r => $"- {r}"));
        }

        if (lore.Count > 0) {
            lines.Add("What you know about what the player mentioned, as you would put it in the game (use it if it helps, in your own words):");
            lines.AddRange(lore.Select(t => $"- [{t.Topic}] {string.Join(" / ", t.Lines)}"));
        }

        if (scene?.Npc?.QuestTopics is { Length: > 0 } questTopics) {
            lines.Add("Quest steps you give in the game's own dialogue (they happen when the player picks the topic there after you open it; the game may first want them to have done something):");
            lines.AddRange(questTopics.Select(DescribeQuestTopic));
        }

        var tasks = _actions.GetOpenTasks(npc.Id);
        if (tasks.Count > 0) {
            lines.Add("Tasks you gave the player that are still open:");
            lines.AddRange(tasks.Select(t => $"- #{t.Id}: {t.Description}{(t.Reward == null ? "" : $" (reward: {t.Reward})")}"));
        }

        var playerLines = new List<string>();
        if (scene?.Player is { } player) {
            var className = string.IsNullOrEmpty(player.ClassName) ? "" : $", {player.ClassName}";
            playerLines.Add($"A {player.Sex.ToLowerInvariant()} {player.Race}{className}, level {player.Level}.");
            if (DescribeHealth(player.HealthRatio) is { } health) {
                playerLines.Add($"They look {health}.");
            }
            if (player.FatigueRatio < 0.25f) {
                playerLines.Add("They look exhausted.");
            }
            if (player.Equipped.Length > 0) {
                playerLines.Add($"They are wearing: {string.Join(", ", player.Equipped)}.");
            }
            if (player.Factions is { Length: > 0 }) {
                var factions = player.Factions.Select(f => f.Rank == null ? f.Name : $"{f.Name} ({f.Rank})");
                playerLines.Add($"They are known as a member of: {string.Join(", ", factions)}.");
            }
            if (player.Bounty > 0) {
                playerLines.Add($"There is a bounty of {player.Bounty} gold on their head.");
            }
            if (player.Quests is { Length: > 0 }) {
                playerLines.Add("Their open quests, from their journal (what they are busy with):");
                playerLines.AddRange(player.Quests.Select(q => string.IsNullOrEmpty(q.Entry) ? $"- {q.Name}" : $"- {q.Name}: {Shorten(q.Entry, 240)}"));
            }
            playerLines.Add($"Their name is {player.Name}, but you only know it if they have introduced themselves.");
        }
        if (!string.IsNullOrWhiteSpace(_config.PlayerBackstory)) {
            playerLines.Add(_config.PlayerBackstory.Trim());
        }
        if (playerLines.Count > 0) {
            lines.Add("");
            lines.Add("THE PLAYER, the person you are talking to:");
            lines.AddRange(playerLines);
        }

        return string.Join("\n", lines);
    }

    // The same for every NPC and every turn, so providers can cache it
    private string BuildRules() => string.Join("\n", [
        "You play a character of The Elder Scrolls III: Morrowind, described below.",
        $"Stay in character. Speak briefly and naturally, as in a real conversation. Never mention being an AI. Always respond in the {_config.ResponseLanguage} language.",
        "You will be told what the people around you say and do. Reply only with your own speech: no narration, no name prefix, no stage directions.",
        "Don't take the player at their word, they may lie. Don't invent characters, items, locations or quests.",
        "When the player walks up to you, greet them in one short line that fits what you remember of them.",
        "Game actions happen only through your tools: if you agree to give, take, follow or fight, call the tool in the same reply as your words. Saying it is not enough, and a tool call is no answer: always say your words too.",
        "Quest orders, quest items and faction matters belong to the game's own dialogue: when the player needs them, say a few words, name the topic to pick if you know it, and call open_dialogue. Never hand out quest items or pretend a quest step happened.",
        "You can only give what you carry, and the game checks that the player really has what they hand you. A refused action shows up in the history as [action failed]: react to it in character.",
        $"If you need something done, you may give the player a task (write it in the {_config.ResponseLanguage} language). When they really did it, pay what you promised and mark it done.",
    ]);

    // - topic "ring", starts the quest "Fargoth's Ring": there you say "...". Their journal then reads: "..."
    private static string DescribeQuestTopic(SceneQuestTopic step) {
        var where = step.Topic == null ? "your greeting when the dialogue opens" : $"topic \"{step.Topic}\"";
        var quest = step.Quest == null ? "the quest" : $"the quest \"{step.Quest}\"";
        var effect = step.Starts ? $"starts {quest}" : step.Finishes ? $"finishes {quest}" : $"moves {quest} on";
        var line = $"- {where}, {effect}";
        if (!string.IsNullOrEmpty(step.Line)) {
            line += $": there you say \"{Shorten(step.Line, 300)}\"";
        }
        if (!string.IsNullOrEmpty(step.Entry)) {
            line += $". Their journal then reads: \"{Shorten(step.Entry, 200)}\"";
        }
        return line;
    }

    // "a male Dark Elf, Guard, Knight of the Imperial Legion, level 12"
    internal static string DescribeNpc(NpcInfo npc, SceneNpcState? state) {
        var parts = new List<string> { $"a {npc.Sex.ToLowerInvariant()} {npc.Race}" };
        if (!string.IsNullOrEmpty(npc.ClassName)) {
            parts.Add(npc.ClassName);
        }
        if (!string.IsNullOrEmpty(npc.Faction)) {
            parts.Add(string.IsNullOrEmpty(npc.FactionRank) ? $"member of the {npc.Faction}" : $"{npc.FactionRank} of the {npc.Faction}");
        }
        if (state is { Level: > 0 }) {
            parts.Add($"level {state.Level}");
        }
        return string.Join(", ", parts);
    }

    private static string Shorten(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength].TrimEnd() + "...";

    private static string DescribeDisposition(int disposition) => disposition switch {
        < 20 => "hostile",
        < 40 => "unfriendly",
        < 60 => "neutral",
        < 80 => "friendly",
        _ => "very friendly",
    };

    private static string? DescribeHealth(float ratio) => ratio switch {
        >= 0.95f => null,
        >= 0.6f => "slightly wounded",
        >= 0.3f => "wounded",
        _ => "badly wounded",
    };

    // How the characters in the history are named in the prompt
    private async Task<Dictionary<string, string>> ResolveNamesAsync(NpcInfo npc, List<StoryEvent> history) {
        var names = new Dictionary<string, string> { [npc.Id] = $"{npc.Name} (you)" };
        foreach (var evt in history) {
            switch (evt) {
                case StoryEvent.PlayerSpeak ps:
                    names.TryAdd(ps.PlayerCharacterId, "Player");
                    break;
                case StoryEvent.PlayerApproach pa:
                    names.TryAdd(pa.PlayerCharacterId, "Player");
                    break;
                case StoryEvent.NpcSpeak ns when !names.ContainsKey(ns.NpcCharacterId):
                    var info = await _npcRepo.GetNpcInfoAsync(ns.NpcCharacterId);
                    names[ns.NpcCharacterId] = info?.Name ?? ns.NpcCharacterId;
                    break;
                case StoryEvent.NpcAction na when !names.ContainsKey(na.NpcCharacterId):
                    var actorInfo = await _npcRepo.GetNpcInfoAsync(na.NpcCharacterId);
                    names[na.NpcCharacterId] = actorInfo?.Name ?? na.NpcCharacterId;
                    break;
            }
        }
        return names;
    }

    private static string DescribeEvent(StoryEvent evt, Dictionary<string, string> names) {
        var line = evt switch {
            StoryEvent.PlayerSpeak ps => $"{NameOf(ps.PlayerCharacterId)}: {ps.Text}",
            StoryEvent.NpcSpeak ns => $"{NameOf(ns.NpcCharacterId)}: {ns.Text}",
            StoryEvent.NpcAction na => $"{NameOf(na.NpcCharacterId)} {StoryEvent.DescribeAction(na)}",
            StoryEvent.PlayerApproach pa => $"{NameOf(pa.PlayerCharacterId)} walks up to {NameOf(pa.NpcCharacterId)}",
            _ => evt.ToString()!,
        };
        // Events recorded before the mod reported game time carry "0"
        return evt.GameTime is "" or "0" ? line : $"[{evt.GameTime}] {line}";

        string NameOf(string id) => names.GetValueOrDefault(id, id);
    }

    private static string? BuildContextBlock(List<StoryEventSummary> summaries, List<StoryEvent> events, Dictionary<string, string> names) {
        var parts = new List<string>();

        if (summaries.Count > 0) {
            parts.Add("PREVIOUS CONVERSATION SUMMARIES:");
            foreach (var summary in summaries) {
                parts.Add(summary.Summary);
            }
        }

        // Include all events except the very last one (which is the current player message)
        var contextEvents = events.Count > 1 ? events[..^1] : [];
        if (contextEvents.Count > 0) {
            parts.Add("RECENT EVENTS:");
            foreach (var evt in contextEvents) {
                parts.Add(DescribeEvent(evt, names));
            }
        }

        return parts.Count > 0 ? string.Join("\n", parts) : null;
    }
}
