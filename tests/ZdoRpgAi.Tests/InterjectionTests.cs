using ZdoRpgAi.ModEmulator;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Server.Llm;
using ZdoRpgAi.Tests.Support;

namespace ZdoRpgAi.Tests;

// Someone standing close who heard an NPC answer the player may cut in with one line
public class InterjectionTests {
    private static DirectorSection Always() => new() { Greetings = false, Banter = false, Interjections = true, InterjectionChance = 1 };

    private static bool Overheard(LlmRequest r) => r.Messages.LastOrDefault()?.Text?.Contains("Nobody spoke to you", StringComparison.Ordinal) == true;

    [Fact]
    public async Task ABystanderWhoHeardTheAnswerCutsIn() {
        // The player talks to Sellus Gravius; Fargoth stands 6 m from him
        var llm = new ScriptedLlm()
            .On(r => ScriptedLlm.IsReplyOf(r, "Fargoth") && Overheard(r), _ => new LlmResponse { Text = "Don't trust him, he took my ring!" })
            .Npc("Sellus Gravius", "Move along, outlander.");
        await using var host = GameHost.Start(SeydaNeen.Create(), llm, new ScriptedLlm(), Always());

        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Have you seen a ring?");

        var lines = host.Game.Events.Where(e => e.Kind == GameEventKind.NpcSaid).ToList();
        Assert.Equal(["sellus_gravius", "fargoth"], lines.Select(l => l.NpcId));
        Assert.Equal("Don't trust him, he took my ring!", lines[1].Text);
        Assert.Equal(EmulatedWorld.PlayerId, lines[1].TargetId);

        // Fargoth spoke from what he heard of Sellus, with no tools to act on
        var request = host.MainLlm.Calls.Single(c => Overheard(c.Request)).Request;
        Assert.Contains("Move along, outlander.", GameHost.TextOf(request));
        Assert.Contains("overheard Sellus Gravius talking with the player", GameHost.TextOf(request));
        Assert.False(request.AllowToolCalls);
    }

    [Fact]
    public async Task ABystanderWithNothingToAddStaysQuiet() {
        var llm = new ScriptedLlm()
            .On(r => Overheard(r), _ => new LlmResponse { Text = "SKIP." })
            .Npc("Sellus Gravius", "Move along.");
        await using var host = GameHost.Start(SeydaNeen.Create(), llm, new ScriptedLlm(), Always());

        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Hello.");

        Assert.Equal(["sellus_gravius"], host.Game.Events.Where(e => e.Kind == GameEventKind.NpcSaid).Select(l => l.NpcId));
        Assert.Single(host.MainLlm.Calls, c => Overheard(c.Request));
    }

    [Fact]
    public async Task NobodyCutsInWhenItIsOff() {
        var llm = new ScriptedLlm().On(r => Overheard(r), _ => new LlmResponse { Text = "Hey!" }).Npc("Sellus Gravius", "Move along.");
        var director = Always();
        director.Interjections = false;
        await using var host = GameHost.Start(SeydaNeen.Create(), llm, new ScriptedLlm(), director);

        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Hello.");

        Assert.DoesNotContain(host.MainLlm.Calls, c => Overheard(c.Request));
    }

    [Fact]
    public async Task NobodyCutsInWithNobodyClose() {
        // Caius Cosades is alone in his house
        var llm = new ScriptedLlm().On(r => Overheard(r), _ => new LlmResponse { Text = "Hey!" }).Npc("Caius Cosades", "Sit down.");
        await using var host = GameHost.Start(SeydaNeen.Create(), llm, new ScriptedLlm(), Always());

        await host.MoveAsync(SeydaNeen.Balmora);
        await host.TargetAsync("caius_cosades");
        await host.SayAsync("I was sent to you.");

        Assert.Equal(["caius_cosades"], host.Game.Events.Where(e => e.Kind == GameEventKind.NpcSaid).Select(l => l.NpcId));
        Assert.DoesNotContain(host.MainLlm.Calls, c => Overheard(c.Request));
    }

    [Fact]
    public async Task NobodyCutsIntoAGreeting() {
        // Sellus remembers chatting with Fargoth in front of the player, so he greets them first
        var llm = new ScriptedLlm()
            .On(r => Overheard(r), _ => new LlmResponse { Text = "Hey!" })
            .Text(r => r.SystemPrompt.StartsWith("You write short ambient conversations", StringComparison.Ordinal),
                """{"lines": [{"speaker": "A", "text": "Quiet day."}, {"speaker": "B", "text": "Too quiet."}]}""")
            .Npc("Sellus Gravius", "You again.");
        var director = Always();
        director.Greetings = true;
        await using var host = GameHost.Start(SeydaNeen.Create(), llm, new ScriptedLlm(), director);

        Assert.True(await host.BanterAsync());
        await host.TargetAsync("sellus_gravius");

        var lines = host.Game.Events.Where(e => e.Kind == GameEventKind.NpcSaid).Select(l => (l.NpcId, l.Text)).ToList();
        Assert.Equal([("sellus_gravius", "Quiet day."), ("fargoth", "Too quiet."), ("sellus_gravius", "You again.")], lines);
        Assert.DoesNotContain(host.MainLlm.Calls, c => Overheard(c.Request));
    }
}
