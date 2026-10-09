using System.Text.Json;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Repository;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Game.Npc;
using ZdoRpgAi.Server.Game.Story;
using ZdoRpgAi.Server.Llm;

namespace ZdoRpgAi.Server.Game.Director;

// Now and then, while the player is nearby but quiet, the two closest NPCs exchange a few lines in
// their own voices. The player speaking cuts them off, like any other NPC speech.
public class BanterScheduler {
    private static readonly ILog Log = Logger.Get<BanterScheduler>();

    private static readonly TimeSpan QuietBeforeBanter = TimeSpan.FromSeconds(30);
    private const float MaxDistanceMeters = 12f;
    private const int MaxLines = 4;
    private const string PlayerId = "player";

    private readonly Director _director;
    private readonly DirectorHelper _directorHelper;
    private readonly NpcRepository _npcRepo;
    private readonly ISaveGameRepository _saveGameRepo;
    private readonly RumorMill _rumors;
    private readonly StoryComposer _storyComposer;
    private readonly ILlm _llm;
    private readonly DirectorSection _config;

    public BanterScheduler(
        Director director, DirectorHelper directorHelper, NpcRepository npcRepo, ISaveGameRepository saveGameRepo,
        RumorMill rumors, StoryComposer storyComposer, ILlm llm, DirectorSection config) {
        _director = director;
        _directorHelper = directorHelper;
        _npcRepo = npcRepo;
        _saveGameRepo = saveGameRepo;
        _rumors = rumors;
        _storyComposer = storyComposer;
        _llm = llm;
        _config = config;
    }

    public async Task RunAsync(CancellationToken ct) {
        if (!_config.Banter) {
            return;
        }

        try {
            while (!ct.IsCancellationRequested) {
                await Task.Delay(TimeSpan.FromSeconds(_config.BanterIntervalSeconds), ct);
                if (DateTime.UtcNow - _storyComposer.LastPlayerActivity < QuietBeforeBanter
                    || Random.Shared.NextDouble() > _config.BanterChance) {
                    continue;
                }

                try {
                    await _director.TryRunExclusiveAsync(ComposeBanterAsync);
                }
                catch (Exception ex) {
                    Log.Warn("Banter failed: {Error}", ex.Message);
                }
            }
        }
        catch (OperationCanceledException) {
            // Server shutting down
        }
    }

    private async Task<List<StoryEvent>> ComposeBanterAsync() {
        var around = await _directorHelper.QueryObserversAsync(PlayerId, null);
        var pair = around.Nearby
            .Where(c => c.DistanceMeters <= MaxDistanceMeters)
            .OrderBy(c => c.DistanceMeters)
            .Take(2)
            .ToArray();
        if (pair.Length < 2) {
            return [];
        }

        if (around.SaveId != null) {
            _saveGameRepo.SetSaveContext(around.SaveId);
        }

        var a = await _npcRepo.GetNpcInfoAsync(pair[0].CharacterId);
        var b = await _npcRepo.GetNpcInfoAsync(pair[1].CharacterId);
        if (a == null || b == null) {
            return [];
        }

        var rumors = await _rumors.GetRumorsAsync(a.Id, around.Cell, includeWitnessed: true);
        var lines = ParseLines((await _llm.ChatAsync(BuildRequest(a, b, around, rumors))).Text);
        Log.Info("Banter between {A} and {B}: {Count} lines", a.Id, b.Id, lines.Count);

        return lines.Select(line => (StoryEvent)StoryEvent.Create(new StoryEvent.NpcSpeak {
            NpcCharacterId = line.ByA ? a.Id : b.Id,
            TargetCharacterId = line.ByA ? b.Id : a.Id,
            GameTime = around.GameTime ?? "0",
            Cell = around.Cell,
            Text = line.Text,
        })).ToList();
    }

    private LlmRequest BuildRequest(NpcInfo a, NpcInfo b, Observers around, List<string> rumors) {
        var context = new List<string> {
            $"A: {a.Name}, {SimpleReactiveStrategy.DescribeNpc(a, null)}. {a.Personality}".TrimEnd(),
            $"B: {b.Name}, {SimpleReactiveStrategy.DescribeNpc(b, null)}. {b.Personality}".TrimEnd(),
        };
        if (around.Cell != null) {
            context.Add($"Place: {around.Cell}.");
        }
        if (around.GameTime != null) {
            context.Add($"Time: {around.GameTime}.");
        }
        if (rumors.Count > 0) {
            context.Add("Recent news around town:");
            context.AddRange(rumors.Select(r => $"- {r}"));
        }

        return new LlmRequest {
            SystemPrompt = $$"""
                You write short ambient conversations between two NPCs of The Elder Scrolls III: Morrowind, overheard by the player passing by.
                Keep it natural and in character: 2 to {{MaxLines}} lines in total, one or two sentences each. They may talk about everyday life, local news and rumors, or the stranger nearby (the player).
                Write the lines in the {{_config.ResponseLanguage}} language.
                Reply with JSON only: {"lines": [{"speaker": "A", "text": "..."}, {"speaker": "B", "text": "..."}]}
                """,
            Messages = [new LlmMessage { Role = LlmRole.User, Text = string.Join("\n", context) }],
        };
    }

    private static List<(bool ByA, string Text)> ParseLines(string? reply) {
        var start = reply?.IndexOf('{') ?? -1;
        var end = reply?.LastIndexOf('}') ?? -1;
        if (start < 0 || end <= start) {
            return [];
        }

        var lines = new List<(bool ByA, string Text)>();
        try {
            foreach (var line in JsonNode.Parse(reply![start..(end + 1)])?["lines"]?.AsArray() ?? []) {
                var speaker = line?["speaker"]?.GetValue<string>();
                var text = line?["text"]?.GetValue<string>()?.Trim();
                if (speaker is "A" or "B" && !string.IsNullOrEmpty(text)) {
                    lines.Add((speaker == "A", text));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) {
            Log.Warn("Could not parse banter: {Error}", ex.Message);
            return [];
        }
        return lines.Take(MaxLines).ToList();
    }
}
