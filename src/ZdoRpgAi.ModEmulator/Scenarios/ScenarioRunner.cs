using System.Text.Json;
using System.Text.Json.Nodes;
using ZdoRpgAi.Core;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Util;

namespace ZdoRpgAi.ModEmulator.Scenarios;

public record CheckResult(string Check, bool Passed, string? Detail = null);

public record StepResult(string Description, IReadOnlyList<GameEvent> Events, IReadOnlyList<CheckResult> Checks, TimeSpan Took, bool TimedOut);

public record ScenarioResult(string Name, string File, IReadOnlyList<StepResult> Steps, string? Error, int LlmCalls, TimeSpan LlmTime) {
    public bool Passed => Error == null && Steps.All(s => !s.TimedOut && s.Checks.All(c => c.Passed));
}

// Plays scenarios one by one, each in a fresh world with fresh memory, and checks what the NPCs
// said and did. The judge is the model that checks plain-word criteria.
public class ScenarioRunner {
    private static readonly ILog Log = Logger.Get<ScenarioRunner>();

    private readonly ILlm _mainLlm;
    private readonly ILlm _simpleLlm;
    private readonly ILlm _judge;
    private readonly DirectorSection _director;

    // Lines to show while scenarios run
    public event Action<string>? Progress;

    public ScenarioRunner(ILlm mainLlm, ILlm simpleLlm, ILlm judge, DirectorSection director) {
        _mainLlm = mainLlm;
        _simpleLlm = simpleLlm;
        _judge = judge;
        _director = director;
    }

    // The models and director settings of a server config. The judge is its fast model.
    public static ScenarioRunner FromServerConfig(string configPath, out string mainProvider) {
        var config = ConfigParser.ParseYamlFile(configPath, ScenarioJsonContext.Default.ServerConfig);
        mainProvider = config.Llm.Main.Provider;
        var simple = ServerBootstrap.CreateLlm("simple", config.Llm.Simple);
        return new ScenarioRunner(ServerBootstrap.CreateLlm("main", config.Llm.Main), simple, simple, config.Director);
    }

    public static bool TryLoad(string yaml, out Scenario? scenario, out string? error) {
        if (!ConfigParser.TryParseYaml(yaml, ScenarioJsonContext.Default.Scenario, out scenario, out error)) {
            return false;
        }
        if (scenario.Steps.Count == 0) {
            error = "no steps";
            return false;
        }
        return true;
    }

    // What would break a run, found before any model is asked: NPCs the world doesn't have,
    // unknown director settings and kinds of action
    public List<string> Check(Scenario scenario) {
        var problems = new List<string>();
        var world = WorldFor(scenario);
        void Npc(string? id, string where) {
            if (id != null && id is not ("" or "none") && !world.Npcs.Any(n => string.Equals(n.ObjectId, id, StringComparison.OrdinalIgnoreCase))) {
                problems.Add($"{where}: no NPC '{id}'");
            }
        }
        try {
            DirectorFor(scenario);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) {
            problems.Add($"director: {ex.Message}");
        }
        for (var i = 0; i < scenario.Steps.Count; i++) {
            var step = scenario.Steps[i];
            var where = $"step {i + 1}";
            Npc(step.Target, where);
            foreach (var e in step.Expect) {
                Npc(e.Replies ?? e.Silent ?? e.Mentions?.Npc ?? e.Avoids?.Npc ?? e.PromptHas?.Npc ?? e.PromptLacks?.Npc ?? e.Action?.Npc ?? e.NoAction?.Npc, where);
                var kind = (e.Action ?? e.NoAction)?.Kind;
                if (kind != null && !Enum.TryParse<GameEventKind>(kind, ignoreCase: true, out _)) {
                    problems.Add($"{where}: no kind of action '{kind}'");
                }
            }
        }
        return problems;
    }

    public async Task<ScenarioResult> RunAsync(Scenario scenario, string file) {
        Report($"▶ {scenario.Name}");
        var steps = new List<StepResult>();
        DirectorSection director;
        try {
            director = DirectorFor(scenario);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) {
            return new ScenarioResult(scenario.Name, file, steps, $"bad director settings: {ex.Message}", 0, TimeSpan.Zero);
        }

        await using var host = GameHost.Start(WorldFor(scenario), _mainLlm, _simpleLlm, director);
        try {
            await host.SettleAsync();
            foreach (var step in scenario.Steps) {
                steps.Add(await RunStepAsync(host, step));
            }
        }
        catch (Exception ex) {
            Log.Error("Scenario {Name} failed: {Error}", scenario.Name, ex.ToString());
            return Finish(new ScenarioResult(scenario.Name, file, steps, ex.Message, 0, TimeSpan.Zero), host);
        }
        return Finish(new ScenarioResult(scenario.Name, file, steps, null, 0, TimeSpan.Zero), host);
    }

    private ScenarioResult Finish(ScenarioResult result, GameHost host) {
        var calls = host.MainLlm.Calls.Concat(host.SimpleLlm.Calls).ToList();
        result = result with { LlmCalls = calls.Count, LlmTime = TimeSpan.FromTicks(calls.Sum(c => c.Took.Ticks)) };
        Report(result.Passed ? "  ✓ passed" : $"  ✗ failed{(result.Error == null ? "" : $": {result.Error}")}");
        return result;
    }

    private DirectorSection DirectorFor(Scenario scenario) {
        var merged = JsonSerializer.SerializeToNode(_director, ScenarioJsonContext.Default.DirectorSection)!.AsObject();
        foreach (var (key, value) in scenario.Director ?? []) {
            var existing = merged.Select(p => p.Key).FirstOrDefault(k => string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"no director setting '{key}'");
            merged[existing] = value?.DeepClone();
        }
        return merged.Deserialize(ScenarioJsonContext.Default.DirectorSection)!;
    }

    private static WorldDefinition WorldFor(Scenario scenario) {
        var world = SeydaNeen.Create();
        var changes = scenario.World;
        if (changes == null) {
            return world;
        }
        if (changes.PlayerFactions != null) {
            world = world with { Player = world.Player with { Factions = [.. changes.PlayerFactions.Select(f => new SceneFaction(f.Name, f.Rank))] } };
        }
        if (changes.Quests != null) {
            world = world with { Quests = [.. changes.Quests.Select(q => new QuestDefinition(q.Name, q.Entry, [.. q.Npcs]))] };
        }
        if (changes.Time != null) {
            world = world with { GameTime = changes.Time };
        }
        return world;
    }

    private async Task<StepResult> RunStepAsync(GameHost host, ScenarioStep step) {
        var eventsBefore = host.Game.Events.Count;
        var callsBefore = host.MainLlm.Calls.Count;
        var started = DateTime.UtcNow;
        var settled = true;
        var description = new List<string>();

        if (step.Target != null) {
            var target = step.Target is "" or "none" ? null : step.Target;
            description.Add(target == null ? "look away" : $"look at {NameOf(host, target)}");
            host.Game.SetTarget(target);
            settled &= await host.SettleAsync();
        }
        if (step.Hours is { } hours) {
            description.Add(FormattableString.Invariant($"{hours:0.#} game hours pass"));
            host.World.PassHours(hours);
        }
        if (step.Go != null) {
            description.Add($"go to {step.Go}");
            host.Game.MoveTo(step.Go);
            settled &= await host.SettleAsync();
        }
        if (step.Say != null) {
            description.Add($"say «{step.Say}»");
            host.Game.Say(step.Say);
            settled &= await host.SettleAsync();
        }
        if (step.Banter) {
            description.Add("let two NPCs nearby talk");
            if (!await host.BanterAsync()) {
                description.Add("(someone was talking, no banter)");
            }
        }
        if (step.Companion) {
            description.Add("let a companion speak up");
            if (!await host.CompanionTalkAsync()) {
                description.Add("(no companion close by)");
            }
        }
        if (step.Wait is { } seconds) {
            description.Add(FormattableString.Invariant($"wait {seconds:0.#} s"));
            await Task.Delay(TimeSpan.FromSeconds(seconds));
            settled &= await host.SettleAsync();
        }

        var events = host.Game.Events.Skip(eventsBefore).ToList();
        var calls = host.MainLlm.Calls.Skip(callsBefore).ToList();
        foreach (var evt in events.Where(e => e.Kind != GameEventKind.PlayerSaid)) {
            Report($"    {Describe(host, evt)}");
        }

        var checks = new List<CheckResult>();
        foreach (var expectation in step.Expect) {
            var check = await CheckAsync(host, expectation, events, calls);
            checks.Add(check);
            Report($"    {(check.Passed ? "✓" : "✗")} {check.Check}{(check.Detail == null ? "" : $" — {check.Detail}")}");
        }
        if (!settled) {
            Report("    ✗ the server was still busy when the step timed out");
        }

        var text = string.Join(", ", description);
        return new StepResult(text.Length == 0 ? "nothing" : text, events, checks, DateTime.UtcNow - started, !settled);
    }

    private async Task<CheckResult> CheckAsync(GameHost host, Expectation e, List<GameEvent> events, List<LlmCall> calls) {
        var lines = events.Where(x => x.Kind == GameEventKind.NpcSaid).ToList();
        if (e.Replies != null) {
            var said = lines.Any(l => Same(l.NpcId, e.Replies));
            return new($"{NameOf(host, e.Replies)} replies", said, said ? null : "said nothing");
        }
        if (e.Silent != null) {
            var said = lines.FirstOrDefault(l => Same(l.NpcId, e.Silent));
            return new($"{NameOf(host, e.Silent)} stays silent", said == null, said == null ? null : $"said «{said.Text}»");
        }
        if (e.Language != null) {
            var wrong = lines.FirstOrDefault(l => !InLanguage(l.Text, e.Language));
            return new($"lines in {e.Language}", lines.Count > 0 && wrong == null,
                lines.Count == 0 ? "no lines" : wrong == null ? null : $"«{wrong.Text}»");
        }
        if (e.Mentions != null || e.Avoids != null) {
            var check = (e.Mentions ?? e.Avoids)!;
            var mine = lines.Where(l => check.Npc == null || Same(l.NpcId, check.Npc)).ToList();
            var hit = mine.FirstOrDefault(l => l.Text.Contains(check.Text, StringComparison.OrdinalIgnoreCase));
            var who = check.Npc == null ? "NPCs" : NameOf(host, check.Npc);
            return e.Mentions != null
                ? new($"{who} mention «{check.Text}»", hit != null, hit == null ? "not said" : null)
                : new($"{who} avoid «{check.Text}»", hit == null, hit == null ? null : $"said «{hit.Text}»");
        }
        if (e.Action != null || e.NoAction != null) {
            var check = (e.Action ?? e.NoAction)!;
            if (!Enum.TryParse<GameEventKind>(check.Kind, ignoreCase: true, out var kind)) {
                return new($"action {check.Kind}", false, "unknown kind of action");
            }
            var done = events.FirstOrDefault(x => x.Kind == kind && x.Success && Same(x.NpcId, check.Npc)
                && (check.Sign == null || (check.Sign == "+" ? x.Value > 0 : x.Value < 0)));
            var what = $"{NameOf(host, check.Npc)}: {check.Kind}{check.Sign}";
            return e.Action != null
                ? new(what, done != null, done == null ? "didn't happen" : null)
                : new($"no {what}", done == null, done == null ? null : Describe(host, done));
        }
        if (e.PromptHas != null || e.PromptLacks != null) {
            var check = (e.PromptHas ?? e.PromptLacks)!;
            var name = NameOf(host, check.Npc ?? "");
            var prompts = host.PromptsOf(check.Npc ?? "", calls);
            var label = e.PromptHas != null ? $"{name} is told «{check.Text}»" : $"{name} isn't told «{check.Text}»";
            if (prompts.Count == 0) {
                return new(label, false, "it wasn't asked for a reply in this step");
            }
            var told = prompts.Any(p => p.Contains(check.Text, StringComparison.OrdinalIgnoreCase));
            return new(label, e.PromptHas != null ? told : !told);
        }
        if (e.Judge != null) {
            return await JudgeAsync(host, e.Judge);
        }
        return new("empty expectation", false, "set one check per item");
    }

    private async Task<CheckResult> JudgeAsync(GameHost host, string criterion) {
        var transcript = string.Join("\n", host.Game.Events.Select(e => Describe(host, e)));
        var response = await _judge.ChatAsync(new LlmRequest {
            SystemPrompt = """
                You check a conversation from The Elder Scrolls III: Morrowind, where the NPCs are played by an AI.
                Judge only the criterion given, about how the conversation ends, strictly but fairly.
                Reply with JSON only: {"pass": true or false, "reason": "one short sentence"}
                """,
            Messages = [new LlmMessage { Role = LlmRole.User, Text = $"Conversation:\n{transcript}\n\nCriterion: {criterion}" }],
        });
        var verdict = ParseVerdict(response.Text);
        return verdict == null
            ? new($"judge: {criterion}", false, $"no verdict from the judge: {response.Error ?? response.Text ?? "empty"}")
            : new($"judge: {criterion}", verdict.Value.Pass, verdict.Value.Reason);
    }

    private static (bool Pass, string? Reason)? ParseVerdict(string? text) {
        var start = text?.IndexOf('{') ?? -1;
        var end = text?.LastIndexOf('}') ?? -1;
        if (start < 0 || end <= start) {
            return null;
        }
        try {
            var json = JsonNode.Parse(text![start..(end + 1)]);
            var pass = json?["pass"]?.GetValue<bool>();
            return pass == null ? null : (pass.Value, json?["reason"]?.GetValue<string>());
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) {
            return null;
        }
    }

    // Cyrillic or Latin letters, judging by the language's alphabet; other alphabets aren't checked
    private static bool InLanguage(string text, string language) {
        var letters = text.Where(char.IsLetter).ToList();
        if (letters.Count == 0) {
            return true;
        }
        var cyrillic = letters.Count(c => c is >= 'Ѐ' and <= 'ӿ') / (double)letters.Count;
        return language.ToLowerInvariant() switch {
            "russian" or "ukrainian" or "belarusian" or "bulgarian" or "serbian" => cyrillic >= 0.6,
            "english" or "german" or "french" or "spanish" or "italian" or "polish" or "portuguese" => cyrillic < 0.1,
            _ => true,
        };
    }

    public static string Describe(GameHost host, GameEvent e) {
        var npc = e.NpcId == null ? "" : NameOf(host, e.NpcId);
        var refused = e.Success ? "" : " (refused)";
        return e.Kind switch {
            GameEventKind.PlayerSaid => e.TargetId == null ? $"Player: {e.Text}" : $"Player (to {NameOf(host, e.TargetId)}): {e.Text}",
            GameEventKind.NpcSaid => e.TargetId is null or EmulatedWorld.PlayerId ? $"{npc}: {e.Text}" : $"{npc} (to {NameOf(host, e.TargetId)}): {e.Text}",
            GameEventKind.Gave => $"[{npc} gives the player {e.Text} x{e.Value}{refused}]",
            GameEventKind.Took => $"[{npc} takes {e.Text} x{e.Value} from the player{refused}]",
            GameEventKind.Disposition => $"[{npc}'s disposition {e.Value:+0;-0;0}{(e.Text.Length == 0 ? "" : $": {e.Text}")}]",
            GameEventKind.Follow => $"[{npc} follows the player{refused}]",
            GameEventKind.StopFollow => $"[{npc} stops following the player]",
            GameEventKind.Attack => $"[{npc} attacks the player]",
            GameEventKind.StopAttack => $"[{npc} stops attacking]",
            GameEventKind.TaskGiven => $"[{npc} gives a task: {e.Text}]",
            GameEventKind.TaskUpdated => $"[{npc}'s quest moves on: {e.Text}]",
            GameEventKind.TaskDone => $"[{npc} marks a task done: {e.Text}]",
            GameEventKind.TaskCancelled => $"[{npc} calls off a task: {e.Text}]",
            GameEventKind.Dialogue => $"[{npc} opens the game's dialogue]",
            GameEventKind.Service => $"[{npc} opens {e.Text}{refused}]",
            GameEventKind.MessageBox => $"[message: {e.Text}]",
            GameEventKind.Moved => $"[the player goes to {e.Text}]",
            _ => $"[{e.Kind} {npc} {e.Text}]",
        };
    }

    private static string NameOf(GameHost host, string id) => host.World.FindNpc(id)?.Name ?? id;

    private static bool Same(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private void Report(string line) => Progress?.Invoke(line);
}
