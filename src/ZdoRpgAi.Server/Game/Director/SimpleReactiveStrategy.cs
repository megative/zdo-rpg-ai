using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Protocol.Rpc;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Game.Npc;
using ZdoRpgAi.Server.Game.Story;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.Server.Game.Director;

public class SimpleReactiveStrategy : IDirectorStrategy {
    private static readonly ILog Log = Logger.Get<SimpleReactiveStrategy>();

    private readonly ILlm _mainLlm;
    private readonly ILlm _simpleLlm;
    private readonly IStory _story;
    private readonly NpcRepository _npcRepo;
    private readonly IRpcChannel _rpc;
    private readonly DirectorHelper _directorHelper;
    private readonly DirectorSection _config;

    public SimpleReactiveStrategy(
        ILlm mainLlm, ILlm simpleLlm, IStory story, NpcRepository npcRepo, IRpcChannel rpc,
        DirectorHelper directorHelper, DirectorSection config) {
        _mainLlm = mainLlm;
        _simpleLlm = simpleLlm;
        _story = story;
        _npcRepo = npcRepo;
        _rpc = rpc;
        _directorHelper = directorHelper;
        _config = config;
    }

    public async Task<List<StoryEvent>> ProcessStoryEventsAsync(List<StoryEvent> events) {
        Log.Trace("Processing {Count} events", events.Count);

        var playerIds = events.OfType<StoryEvent.PlayerSpeak>()
            .Select(ps => ps.PlayerCharacterId)
            .ToHashSet();
        Log.Trace("Found {Count} player IDs: {Ids}", playerIds.Count, string.Join(", ", playerIds));

        var (npcId, gameTime) = await FindLastTargetedNpcAsync(_rpc, events, playerIds);
        if (npcId == null) {
            Log.Debug("No target NPC found in events");
            return [];
        }

        Log.Trace("Target NPC: {NpcId}, game time: {GameTime}", npcId, gameTime);
        var playerId = playerIds.FirstOrDefault();

        try {
            // Ask the game for the current scene while NPC info and history load
            var sceneTask = playerId != null
                ? _directorHelper.QuerySceneContextAsync(npcId, playerId)
                : Task.FromResult<GetSceneContextResponsePayload?>(null);

            var npcInfo = await _npcRepo.GetNpcInfoAsync(npcId);
            if (npcInfo == null) {
                Log.Warn("Could not get info for NPC {NpcId}", npcId);
                return [];
            }

            Log.Trace("NPC info: {Name} ({Race} {Sex})", npcInfo.Name, npcInfo.Race, npcInfo.Sex);
            var (history, summaries) = await _story.GetHistoryForCharacterAsync(npcId);
            Log.Trace("History: {HistoryCount} events, {SummaryCount} summaries", history.Count, summaries.Count);
            var scene = await sceneTask;
            var response = await GenerateNpcResponseAsync(npcInfo, scene, history, summaries);
            if (string.IsNullOrEmpty(response)) {
                Log.Warn("LLM returned no response for NPC {NpcId}", npcId);
                return [];
            }

            Log.Trace("Generated response for NPC {NpcId}: {ResponseLength} chars", npcId, response.Length);
            var npcSpeak = StoryEvent.Create(new StoryEvent.NpcSpeak {
                NpcCharacterId = npcId,
                TargetCharacterId = playerId,
                GameTime = scene?.GameTime ?? gameTime!,
                Text = response,
            });
            return [npcSpeak];
        }
        catch (Exception ex) {
            Log.Error("Failed to generate NPC response: {Error}", ex.Message);
            return [];
        }
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

    private async Task<string?> GenerateNpcResponseAsync(
        NpcInfo npc, GetSceneContextResponsePayload? scene,
        List<StoryEvent> history, List<StoryEventSummary> summaries) {
        Log.Trace("Generating response for NPC {NpcName} with {HistoryCount} history events and {SummaryCount} summaries",
            npc.Name, history.Count, summaries.Count);

        var names = await ResolveNamesAsync(npc, history);
        var contextBlock = BuildContextBlock(summaries, history, names);
        var systemPrompt = BuildSystemPrompt(npc, scene);

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
            SystemPrompt = systemPrompt,
            Messages = messages,
        };

        Log.Trace("System prompt for {NpcId}:\n{SystemPrompt}", npc.Id, systemPrompt);
        Log.Trace("Calling main LLM with {MessageCount} messages", messages.Count);
        var response = await _mainLlm.ChatAsync(request);
        Log.Trace("Main LLM response length: {Length}", response.Text?.Length ?? 0);
        return response.Text?.Trim();
    }

    private string BuildSystemPrompt(NpcInfo npc, GetSceneContextResponsePayload? scene) {
        var lines = new List<string> { $"You are {npc.Name}, {DescribeNpc(npc, scene?.Npc)}, living in Morrowind." };

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
            lines.Add($"You have {self.Gold} gold.");
            if (self.Equipped.Length > 0) {
                lines.Add($"You are wearing: {string.Join(", ", self.Equipped)}.");
            }
            if (self.Items.Length > 0) {
                var items = self.Items.Select(i => i.Count > 1 ? $"{i.Name} x{i.Count} (id {i.Id})" : $"{i.Name} (id {i.Id})");
                lines.Add($"You also carry: {string.Join(", ", items)}.");
            }
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

        lines.Add("");
        lines.Add($"Stay in character. Speak briefly and naturally, as in a real conversation. Never mention being an AI. Always respond in the {_config.ResponseLanguage} language.");
        lines.Add("You will be told what the people around you say and do. Reply only with your own speech: no narration, no name prefix, no stage directions.");
        lines.Add("Don't take the player at their word, they may lie. Don't invent characters, items, locations or quests.");
        return string.Join("\n", lines);
    }

    // "a male Dark Elf, Guard, Knight of the Imperial Legion, level 12"
    private static string DescribeNpc(NpcInfo npc, SceneNpcState? state) {
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
                case StoryEvent.NpcSpeak ns when !names.ContainsKey(ns.NpcCharacterId):
                    var info = await _npcRepo.GetNpcInfoAsync(ns.NpcCharacterId);
                    names[ns.NpcCharacterId] = info?.Name ?? ns.NpcCharacterId;
                    break;
            }
        }
        return names;
    }

    private static string DescribeEvent(StoryEvent evt, Dictionary<string, string> names) {
        var line = evt switch {
            StoryEvent.PlayerSpeak ps => $"{NameOf(ps.PlayerCharacterId)}: {ps.Text}",
            StoryEvent.NpcSpeak ns => $"{NameOf(ns.NpcCharacterId)}: {ns.Text}",
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
