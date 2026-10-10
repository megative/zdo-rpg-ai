using System.Text.Json.Nodes;
using ZdoRpgAi.ModEmulator.Scenarios;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Tests.Support;

namespace ZdoRpgAi.Tests;

public class ScenarioRunnerTests {
    private const string Ring = """
        name: Фаргот получает кольцо
        director:
          greetings: false
        steps:
          - target: fargoth
            say: Я нашёл твоё кольцо!
            expect:
              - replies: fargoth
              - language: Russian
              - mentions: { npc: fargoth, text: кольцо }
              - action: { npc: fargoth, kind: disposition, sign: "+" }
              - noAction: { npc: fargoth, kind: attack }
              - promptLacks: { npc: fargoth, text: Report to Caius Cosades }
              - judge: Фаргот рад
          - target: sellus_gravius
            say: Привет.
            expect:
              - replies: sellus_gravius
              - silent: fargoth
              - promptHas: { npc: sellus_gravius, text: Report to Caius Cosades }
        """;

    private static ScenarioRunner Runner(out ScriptedLlm judge) {
        var main = new ScriptedLlm()
            .Npc("Fargoth", "Моё кольцо! Спасибо тебе, друг!",
                ("change_disposition", new JsonObject { ["delta"] = 10, ["reason"] = "вернул кольцо" }))
            .Npc("Sellus Gravius", "Проходи, не задерживайся.");
        judge = new ScriptedLlm().Text(r => r.SystemPrompt.StartsWith("You check a conversation"), """{"pass": true, "reason": "он благодарит"}""");
        return new ScenarioRunner(main, new ScriptedLlm(), judge, new DirectorSection { Banter = false, Interjections = false });
    }

    [Fact]
    public async Task PassesWhenEverythingHappens() {
        Assert.True(ScenarioRunner.TryLoad(Ring, out var scenario, out var error), error);
        var result = await Runner(out _).RunAsync(scenario!, "ring.yaml");

        var failed = result.Steps.SelectMany(s => s.Checks).Where(c => !c.Passed).Select(c => $"{c.Check}: {c.Detail}");
        Assert.True(result.Passed, string.Join("; ", failed));
        Assert.Equal(7, result.Steps[0].Checks.Count);
        Assert.Contains(result.Steps[0].Events, e => e.Text == "Моё кольцо! Спасибо тебе, друг!");
        Assert.True(result.LlmCalls >= 2);
    }

    [Fact]
    public async Task FailsOnWhatDidntHappenAndSaysWhy() {
        var yaml = """
            name: Ждём не того
            steps:
              - target: fargoth
                say: Привет.
                expect:
                  - replies: arrille
                  - avoids: { text: кольцо }
                  - language: English
            """;
        Assert.True(ScenarioRunner.TryLoad(yaml, out var scenario, out _));
        var result = await Runner(out _).RunAsync(scenario!, "wrong.yaml");

        Assert.False(result.Passed);
        var checks = result.Steps[0].Checks;
        Assert.Equal("said nothing", checks[0].Detail);
        Assert.False(checks[1].Passed);
        Assert.False(checks[2].Passed);
    }

    [Fact]
    public void RejectsAScenarioWithoutSteps() {
        Assert.False(ScenarioRunner.TryLoad("name: Пусто\n", out _, out var error));
        Assert.Equal("no steps", error);
    }

    [Fact]
    public async Task StopsOnAnUnknownDirectorSetting() {
        Assert.True(ScenarioRunner.TryLoad("name: x\ndirector:\n  nope: 1\nsteps:\n  - say: hi\n", out var scenario, out _));
        var result = await Runner(out _).RunAsync(scenario!, "x.yaml");
        Assert.False(result.Passed);
        Assert.Contains("nope", result.Error);
    }

    public static TheoryData<string> ScenarioFiles() =>
        new(Directory.GetFiles(Path.Combine(TestSetup.RepoRoot, "eval", "scenarios"), "*.yaml").Select(Path.GetFileName).OfType<string>());

    // The shipped scenarios load, name only what the world has and play to the end; whether a
    // stand-in model passes them doesn't matter here
    [Theory]
    [MemberData(nameof(ScenarioFiles))]
    public async Task ShippedScenariosPlay(string file) {
        var yaml = File.ReadAllText(Path.Combine(TestSetup.RepoRoot, "eval", "scenarios", file));
        Assert.True(ScenarioRunner.TryLoad(yaml, out var scenario, out var error), error);
        var runner = Runner(out _);
        Assert.Empty(runner.Check(scenario!));

        var result = await runner.RunAsync(scenario!, file);
        Assert.Null(result.Error);
        Assert.DoesNotContain(result.Steps, s => s.TimedOut);
        Assert.Equal(scenario!.Steps.Count, result.Steps.Count);
    }

    [Fact]
    public void FindsTyposBeforeRunning() {
        var yaml = """
            name: Опечатки
            director: { banterr: true }
            steps:
              - target: fargot
                expect:
                  - action: { npc: arrille, kind: hug }
            """;
        Assert.True(ScenarioRunner.TryLoad(yaml, out var scenario, out _));
        var problems = Runner(out _).Check(scenario!);
        Assert.Contains(problems, p => p.Contains("banterr"));
        Assert.Contains("step 1: no NPC 'fargot'", problems);
        Assert.Contains("step 1: no kind of action 'hug'", problems);
    }

    [Fact]
    public void ReportsInMarkdown() {
        var result = new ScenarioResult("Кольцо", "ring.yaml", [
            new StepResult("say «hi»", [new(ModEmulator.GameEventKind.NpcSaid, "fargoth", "Hello | there")],
                [new CheckResult("Fargoth replies", true), new CheckResult("lines in Russian", false, "«Hello | there»")], TimeSpan.FromSeconds(1.2), false),
        ], null, 3, TimeSpan.FromSeconds(2));
        var md = ScenarioReport.ToMarkdown([result], "Main model provider: claude");
        Assert.Contains("# Scenarios: 0 of 1 passed", md);
        Assert.Contains("| Кольцо | ✗ | 3 | 2.0 s |", md);
        Assert.Contains("**fargoth:** Hello \\| there", md);
        Assert.Contains("✗ lines in Russian: «Hello \\| there»", md);
    }
}
