using ZdoRpgAi.Repository;

namespace ZdoRpgAi.Tests;

// Loading an earlier save turns the game clock back; NPCs forget what came after it
public class SaveTimelineTests : IDisposable {
    private readonly string _dir = Directory.CreateTempSubdirectory("zdo-timeline-").FullName;
    private readonly LocalDatabaseSaveGameRepository _repo;

    public SaveTimelineTests() {
        _repo = new LocalDatabaseSaveGameRepository(Path.Combine(_dir, "save.db"));
    }

    public void Dispose() {
        _repo.Dispose();
        Directory.Delete(_dir, recursive: true);
    }

    private long Event(double seconds) {
        var id = _repo.AddStoryEvent("t", "r", "NpcSpeak", $"{{\"gameSeconds\":{seconds}}}", seconds);
        _repo.AddStoryEventObservers(id, ["teruise"]);
        return id;
    }

    [Fact]
    public void ForgetsWhatCameAfterTheLoadedMoment() {
        var early = Event(100);
        var archivedEarly = Event(120);
        var archivedLate = Event(300);
        var late = Event(310);
        var summary = _repo.AddStoryEventSummary("knows about the cellar", "r", ["teruise"]);
        _repo.ArchiveStoryEvents([archivedEarly, archivedLate], summary);

        var keptTask = _repo.AddNpcTask("teruise", "before", null, "t", 50);
        _repo.CloseNpcTask(keptTask, "teruise", NpcTaskStatus.Done, "t", 280);
        var cellarTask = _repo.AddNpcTask("teruise", "rats in the cellar", "1 gold", "t", 250);
        var quest = _repo.AddNpcTask("teruise", "a quest", null, "t", 60);
        _repo.AddQuestSteps(quest, [("fargoth", "talk to Fargoth"), ("teruise", "come back")]);
        _repo.MarkQuestStepDone(quest, 0, 260);
        _repo.AddCompanion("fargoth", "t", 270);
        _repo.AddCompanion("guard", "t", 90);

        Assert.Equal(2, _repo.ForgetAfter(200));

        var events = _repo.GetActiveStoryEventsForCharacter("teruise").Select(e => e.Id).ToList();
        Assert.Equal([early, archivedEarly], events.Order());
        Assert.Empty(_repo.GetActiveSummariesForCharacter("teruise"));
        Assert.Null(_repo.GetOpenNpcTask(cellarTask));
        Assert.NotNull(_repo.GetOpenNpcTask(keptTask));
        Assert.False(_repo.GetQuestSteps(quest)[0].Done);
        Assert.Equal(["guard"], _repo.GetCompanions());
    }

    [Fact]
    public void NothingLaterChangesNothing() {
        var e = Event(100);
        Assert.Equal(0, _repo.ForgetAfter(500));
        Assert.Single(_repo.GetActiveStoryEventsForCharacter("teruise"), x => x.Id == e);
    }
}
