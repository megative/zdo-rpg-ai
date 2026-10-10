using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using ZdoRpgAi.ModEmulator;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Tests.Support;

namespace ZdoRpgAi.Tests;

// Alpha: small quests whose steps send the player to people they have met, then back to the giver
public partial class QuestChainTests {
    private static DirectorSection Settings(bool questChains = true) =>
        new() { Greetings = false, Banter = false, Interjections = false, Companions = false, QuestChains = questChains };

    private static string Text(LlmRequest r) => GameHost.TextOf(r);

    // The quest number the NPC was told about
    private static int QuestId(LlmRequest r) => int.Parse(QuestNumber().Match(Text(r)).Groups[1].Value);

    [GeneratedRegex(@"#(\d+)")]
    private static partial Regex QuestNumber();

    private static JsonObject StartQuest(string stepNpc = "caius_cosades") => new() {
        ["title"] = "Весть для Кая",
        ["step1_npc"] = stepNpc,
        ["step1_goal"] = "передать Каю Косадесу в Балморе, что посылка в пути",
        ["final_goal"] = "вернуться к Селлусу с ответом",
        ["reward"] = "30 золота",
    };

    private static ScriptedLlm Script(string stepNpc = "caius_cosades") => new ScriptedLlm()
        .On(r => ScriptedLlm.IsReplyOf(r, "Sellus Gravius") && Text(r).Contains("they have done every step"),
            r => ScriptedLlm.Reply("Хорошая работа. Держи.",
                ("complete_task", new JsonObject { ["task_id"] = QuestId(r) }),
                ("give_item", new JsonObject { ["item_id"] = EmulatedWorld.GoldId, ["count"] = 30 })))
        .On(r => ScriptedLlm.IsReplyOf(r, "Sellus Gravius") && Text(r).Contains("Player: Готово!"),
            r => ScriptedLlm.Reply("Не ври мне.", ("complete_task", new JsonObject { ["task_id"] = QuestId(r) })))
        .On(r => ScriptedLlm.IsReplyOf(r, "Sellus Gravius"),
            _ => ScriptedLlm.Reply("Есть дело. Отнеси весть Каю.", ("start_quest", StartQuest(stepNpc))))
        .On(r => ScriptedLlm.IsReplyOf(r, "Caius Cosades") && Text(r).Contains("The player is on quest #"),
            r => ScriptedLlm.Reply("Передай Селлусу: понял.", ("advance_quest", new JsonObject { ["quest_id"] = QuestId(r) })))
        .Npc("Caius Cosades", "Чего тебе?")
        .On(r => ScriptedLlm.IsReplyOf(r, "Fargoth") && Text(r).Contains("Player: Ну, сделал?"),
            _ => ScriptedLlm.Reply("Что сделал?", ("advance_quest", new JsonObject { ["quest_id"] = 1 })))
        .Npc("Fargoth", "Что?")
        .Npc("Hrisskar Flat-Foot", "Не слышал.");

    private static async Task MeetCaiusAsync(GameHost host) {
        await host.MoveAsync(SeydaNeen.Balmora);
        await host.TargetAsync("caius_cosades");
        await host.SayAsync("Здравствуй, Кай.");
        await host.MoveAsync(SeydaNeen.Cell);
    }

    private static List<GameEvent> Of(GameHost host, GameEventKind kind) => host.Game.Events.Where(e => e.Kind == kind).ToList();

    [Fact]
    public async Task AQuestGoesFromTheGiverToSomeoneMetAndBack() {
        await using var host = GameHost.Start(SeydaNeen.Create(), Script(), new ScriptedLlm(), Settings());
        await MeetCaiusAsync(host);

        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Нужна помощь?");

        var sellus = host.PromptsOf("sellus_gravius").Last();
        Assert.Contains("PEOPLE THE PLAYER HAS MET", sellus);
        Assert.Contains($"Caius Cosades (caius_cosades), Imperial Monk, last seen in {SeydaNeen.Balmora}", sellus);
        var given = Assert.Single(Of(host, GameEventKind.TaskGiven));
        Assert.Equal("Весть для Кая: передать Каю Косадесу в Балморе, что посылка в пути (30 золота)", given.Text);

        // Not done before the player has been to Caius
        await host.SayAsync("Готово!");
        Assert.Empty(Of(host, GameEventKind.TaskDone));

        // Only those in the quest know of it, and whoever stood close enough to hear it given:
        // Fargoth, 6 m from Sellus, did; Hrisskar, 60 m off, didn't
        await host.TargetAsync("hrisskar_flat-foot");
        await host.SayAsync("Слышал про весть для Кая?");
        Assert.DoesNotContain("Весть для Кая\"", host.PromptsOf("hrisskar_flat-foot").Last());

        await host.MoveAsync(SeydaNeen.Balmora);
        await host.TargetAsync("caius_cosades");
        await host.SayAsync("Селлус просил передать: посылка в пути.");
        Assert.Contains("on quest #1 \"Весть для Кая\" from Sellus Gravius: now they are to передать Каю Косадесу", host.PromptsOf("caius_cosades").Last());
        var updated = Assert.Single(Of(host, GameEventKind.TaskUpdated));
        Assert.Equal("sellus_gravius", updated.NpcId);
        Assert.StartsWith("Весть для Кая: вернуться к Селлусу с ответом", updated.Text);

        await host.MoveAsync(SeydaNeen.Cell);
        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Кай всё понял.");
        Assert.Single(Of(host, GameEventKind.TaskDone));
        Assert.Equal(130, host.World.CountOf(EmulatedWorld.PlayerId, EmulatedWorld.GoldId));
    }

    [Fact]
    public async Task OnlyTheNpcOfTheStepCanMoveItOn() {
        await using var host = GameHost.Start(SeydaNeen.Create(), Script(), new ScriptedLlm(), Settings());
        await MeetCaiusAsync(host);
        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Нужна помощь?");

        await host.TargetAsync("fargoth");
        await host.SayAsync("Ну, сделал?");
        await host.SayAsync("Ладно.");

        Assert.Empty(Of(host, GameEventKind.TaskUpdated));
        Assert.Contains("[action failed: the player's step in it isn't with you] moves quest #1 on", FailedActions(host));
    }

    [Fact]
    public async Task AQuestCanOnlySendThePlayerToSomeoneTheyHaveMet() {
        // Arrille never talked with the player
        await using var host = GameHost.Start(SeydaNeen.Create(), Script(stepNpc: "arrille"), new ScriptedLlm(), Settings());
        await MeetCaiusAsync(host);

        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Нужна помощь?");
        await host.SayAsync("И кого искать?");

        Assert.Empty(Of(host, GameEventKind.TaskGiven));
        Assert.Contains("[action failed: arrille is not someone else the player has met]", FailedActions(host));
    }

    [Fact]
    public async Task WithoutTheSwitchThereAreNoQuests() {
        await using var host = GameHost.Start(SeydaNeen.Create(), Script(), new ScriptedLlm(), Settings(questChains: false));
        await MeetCaiusAsync(host);

        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Нужна помощь?");

        var request = host.MainLlm.Calls.Last(c => ScriptedLlm.IsReplyOf(c.Request, "Sellus Gravius")).Request;
        Assert.DoesNotContain(request.Tools, t => t.Name is "start_quest" or "advance_quest");
        Assert.DoesNotContain("PEOPLE THE PLAYER HAS MET", Text(request));
        Assert.Empty(Of(host, GameEventKind.TaskGiven));
    }

    // What the NPCs were told came of their actions, in their later prompts
    private static string FailedActions(GameHost host) =>
        string.Join("\n", host.MainLlm.Calls.SelectMany(c => c.Request.Messages).Select(m => m.Text).OfType<string>()
            .Where(t => t.Contains("[action failed", StringComparison.Ordinal)));
}
