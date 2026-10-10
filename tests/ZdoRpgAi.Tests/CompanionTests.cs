using System.Text.Json.Nodes;
using ZdoRpgAi.ModEmulator;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Tests.Support;

namespace ZdoRpgAi.Tests;

// Whoever agreed to follow the player talks on the road
public class CompanionTests {
    private static DirectorSection Settings() => new() { Greetings = false, Banter = false, Interjections = false };

    private static bool Asked(LlmRequest r, string situation) =>
        r.Messages.LastOrDefault()?.Text?.Contains(situation, StringComparison.Ordinal) == true;

    // Hrisskar agrees to come along when asked, and has a word for the road
    private static ScriptedLlm Hrisskar(string arrival = "Balmora. My feet hurt.", string quiet = "Long road, this.") => new ScriptedLlm()
        .On(r => ScriptedLlm.IsReplyOf(r, "Hrisskar Flat-Foot") && Asked(r, "have just arrived"), _ => new LlmResponse { Text = arrival })
        .On(r => ScriptedLlm.IsReplyOf(r, "Hrisskar Flat-Foot") && Asked(r, "quiet for a while"), _ => new LlmResponse { Text = quiet })
        .On(r => ScriptedLlm.IsReplyOf(r, "Hrisskar Flat-Foot") && GameHost.TextOf(r).Contains("go home", StringComparison.Ordinal),
            _ => ScriptedLlm.Reply("Fine, I'm off.", ("stop_following", new JsonObject())))
        .Npc("Hrisskar Flat-Foot", "Lead the way.", ("follow_player", new JsonObject()));

    private static async Task HireHrisskarAsync(GameHost host) {
        await host.TargetAsync("hrisskar_flat-foot");
        await host.SayAsync("Come with me to Balmora.");
        Assert.True(host.World.IsFollowing("hrisskar_flat-foot"));
    }

    private static List<string> LinesOf(GameHost host, string npcId) =>
        host.Game.Events.Where(e => e.Kind == GameEventKind.NpcSaid && e.NpcId == npcId).Select(e => e.Text).ToList();

    [Fact]
    public async Task ACompanionHasAWordOnArrivingInAnotherTown() {
        await using var host = GameHost.Start(SeydaNeen.Create(), Hrisskar(), new ScriptedLlm(), Settings());
        await HireHrisskarAsync(host);

        await host.MoveAsync(SeydaNeen.Balmora);

        Assert.Equal(["Lead the way.", "Balmora. My feet hurt."], LinesOf(host, "hrisskar_flat-foot"));
        var request = host.MainLlm.Calls.Last(c => Asked(c.Request, "have just arrived")).Request;
        Assert.Contains($"have just arrived with them at {SeydaNeen.Balmora}", GameHost.TextOf(request));
        Assert.Contains("You travel with the player as their companion", request.SystemPrompt);
        Assert.Contains("Come with me to Balmora.", GameHost.TextOf(request));
        Assert.False(request.AllowToolCalls);
    }

    [Fact]
    public async Task ADoorWithinTheSameTownGetsNoWord() {
        await using var host = GameHost.Start(SeydaNeen.Create(), Hrisskar(), new ScriptedLlm(), Settings());
        await HireHrisskarAsync(host);

        await host.MoveAsync("Seyda Neen, Arrille's Tradehouse");

        Assert.Equal(["Lead the way."], LinesOf(host, "hrisskar_flat-foot"));
    }

    [Fact]
    public async Task ACompanionWhoLeftStaysQuiet() {
        await using var host = GameHost.Start(SeydaNeen.Create(), Hrisskar(), new ScriptedLlm(), Settings());
        await HireHrisskarAsync(host);
        await host.SayAsync("Actually, go home.");
        Assert.False(host.World.IsFollowing("hrisskar_flat-foot"));

        await host.MoveAsync(SeydaNeen.Balmora);

        Assert.False(await host.CompanionTalkAsync());
        Assert.Equal(["Lead the way.", "Fine, I'm off."], LinesOf(host, "hrisskar_flat-foot"));
    }

    [Fact]
    public async Task ACompanionSpeaksUpOnAQuietRoad() {
        await using var host = GameHost.Start(SeydaNeen.Create(), Hrisskar(), new ScriptedLlm(), Settings());
        await HireHrisskarAsync(host);

        Assert.True(await host.CompanionTalkAsync());

        Assert.Equal(["Lead the way.", "Long road, this."], LinesOf(host, "hrisskar_flat-foot"));
        Assert.Equal(EmulatedWorld.PlayerId, host.Game.Events.Last(e => e.Kind == GameEventKind.NpcSaid).TargetId);
    }

    [Fact]
    public async Task ACompanionWithNothingToSaySkips() {
        await using var host = GameHost.Start(SeydaNeen.Create(), Hrisskar(arrival: "SKIP"), new ScriptedLlm(), Settings());
        await HireHrisskarAsync(host);

        await host.MoveAsync(SeydaNeen.Balmora);

        Assert.Equal(["Lead the way."], LinesOf(host, "hrisskar_flat-foot"));
    }

    [Fact]
    public async Task NoCompanionsNoTalk() {
        await using var host = GameHost.Start(SeydaNeen.Create(), Hrisskar(), new ScriptedLlm(), Settings());

        await host.MoveAsync(SeydaNeen.Balmora);

        Assert.False(await host.CompanionTalkAsync());
        Assert.Empty(host.MainLlm.Calls);
    }

    [Fact]
    public async Task CompanionsCanBeTurnedOff() {
        var settings = Settings();
        settings.Companions = false;
        await using var host = GameHost.Start(SeydaNeen.Create(), Hrisskar(), new ScriptedLlm(), settings);
        await HireHrisskarAsync(host);

        await host.MoveAsync(SeydaNeen.Balmora);

        Assert.Equal(["Lead the way."], LinesOf(host, "hrisskar_flat-foot"));
    }
}
