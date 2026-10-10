using System.Text.Json.Nodes;
using ZdoRpgAi.ModEmulator;
using ZdoRpgAi.Protocol.Messages;
using ZdoRpgAi.Server.Bootstrap;
using ZdoRpgAi.Tests.Support;

namespace ZdoRpgAi.Tests;

// What an NPC may know about the player: its own part of their quests, a secret faction only as
// a fellow member, what it heard within earshot, and rumors of what others could see
public class KnowledgeTests {
    private static readonly DirectorSection Quiet = new() { Greetings = false, Banter = false, Interjections = false };

    // In Seyda Neen the player stands at the middle: Sellus Gravius 5 m away, Fargoth 11 m,
    // Arrille and Hrisskar some 60 m off by the tradehouse
    private static WorldDefinition BladeInSeydaNeen() {
        var world = SeydaNeen.Create();
        return world with { Player = world.Player with { Factions = [new("Blades", "Novice"), new("Fighters Guild", "Journeyman")] } };
    }

    [Fact]
    public async Task OnlyTheNpcsOfAQuestSeeItInTheJournal() {
        var llm = new ScriptedLlm().Npc("Fargoth", "Never heard of him.").Npc("Sellus Gravius", "Balmora, the South Wall.");
        await using var host = GameHost.Start(BladeInSeydaNeen(), llm, new ScriptedLlm(), Quiet);

        await host.TargetAsync("fargoth");
        await host.SayAsync("Do you know Caius Cosades?");
        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Where do I find Caius Cosades?");

        Assert.DoesNotContain("Report to Caius Cosades", host.PromptsOf("fargoth").Single());
        Assert.Contains("Report to Caius Cosades", host.PromptsOf("sellus_gravius").Single());
    }

    [Fact]
    public async Task OnlyAFellowBladeKnowsThePlayerIsOne() {
        var llm = new ScriptedLlm().Npc("Fargoth", "Hello.").Npc("Caius Cosades", "Sit down.");
        await using var host = GameHost.Start(BladeInSeydaNeen(), llm, new ScriptedLlm(), Quiet);

        await host.TargetAsync("fargoth");
        await host.SayAsync("Hello.");
        await host.MoveAsync(SeydaNeen.Balmora);
        await host.TargetAsync("caius_cosades");
        await host.SayAsync("I was told to report to you.");

        var fargoth = host.PromptsOf("fargoth").Single();
        Assert.DoesNotContain("Blades", fargoth);
        Assert.Contains("member of: Fighters Guild (Journeyman)", fargoth);
        Assert.Contains("member of: Blades (Novice), Fighters Guild (Journeyman)", host.PromptsOf("caius_cosades").Single());
    }

    [Fact]
    public async Task NpcsOutOfEarshotDontRememberWhatThePlayerSaid() {
        var llm = new ScriptedLlm().Npc("Sellus Gravius", "Keep your voice down.").Npc("Fargoth", "Hm?");
        await using var host = GameHost.Start(SeydaNeen.Create(), llm, new ScriptedLlm(), Quiet);

        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("The password is moon-and-star.");
        await host.TargetAsync("fargoth");
        await host.SayAsync("Nice weather.");

        // Fargoth stands 11 m from the player: he hears Sellus, 6 m away, but not the player
        var fargoth = host.PromptsOf("fargoth").Single();
        Assert.DoesNotContain("moon-and-star", fargoth);
        Assert.Contains("Keep your voice down.", fargoth);
    }

    [Fact]
    public async Task RumorsCarryWhatOthersSawButNotPrivateTasksAndTakeTime() {
        var llm = new ScriptedLlm()
            .Npc("Sellus Gravius", "Take this, and bring me a scamp skin.",
                ("give_item", new JsonObject { ["item_id"] = EmulatedWorld.GoldId, ["count"] = 5 }),
                ("give_task", new JsonObject { ["description"] = "Bring Sellus a scamp skin", ["reward"] = "10 gold" }))
            .Npc("Hrisskar Flat-Foot", "What do you want?");
        await using var host = GameHost.Start(SeydaNeen.Create(), llm, new ScriptedLlm(), Quiet);

        await host.TargetAsync("sellus_gravius");
        await host.SayAsync("Any work for me?");
        await host.TargetAsync("hrisskar_flat-foot");
        await host.SayAsync("What's new around here?");

        // Hrisskar was some 60 m away: a moment later he has heard nothing yet
        Assert.DoesNotContain("Sellus Gravius gives the player", host.PromptsOf("hrisskar_flat-foot").Last());

        host.World.PassHours(3);
        await host.SayAsync("And now?");

        // Hours later he knows of the gold from gossip, of the task not at all
        var hrisskar = host.PromptsOf("hrisskar_flat-foot").Last();
        Assert.Contains("Sellus Gravius gives the player 5 gold", hrisskar);
        Assert.DoesNotContain("scamp skin", hrisskar);
        Assert.Contains(host.Game.Events, e => e.Kind == GameEventKind.TaskGiven && e.NpcId == "sellus_gravius");
        Assert.Equal(105, host.World.CountOf(EmulatedWorld.PlayerId, EmulatedWorld.GoldId));
    }

    [Fact]
    public void TheEmulatedClockMovesOn() {
        var world = new EmulatedWorld(SeydaNeen.Create());
        Assert.Equal("09:30, 16 Last Seed 3E 427", world.GameTime);
        world.PassHours(15.25);
        Assert.Equal("00:45, 17 Last Seed 3E 427", world.GameTime);
        Assert.Equal((16 * 24 + 0.75) * 3600, world.GameSeconds);
    }
}
