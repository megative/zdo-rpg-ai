namespace ZdoRpgAi.Repository;

public interface ISaveGameRepository : IDisposable {
    // The playthrough whose memory is active: the save id the mod reported, "default" before that
    string SaveId { get; }
    // Switches to the database of another playthrough; returns false if it is already active
    bool SetSaveContext(string saveId);
    long AddStoryEvent(string gameTime, string realTime, string type, string dataJson, double? gameSeconds = null);
    void AddStoryEventObservers(long storyEventId, string[] characterIds);
    List<RawStoryEvent> GetActiveStoryEventsForCharacter(string characterId);
    // Latest events of a type, newest first, archived ones included. excludeCharacterId skips events
    // that character performed or witnessed.
    List<RawStoryEvent> GetRecentEvents(string type, string? excludeCharacterId, int limit);
    List<RawStoryEventSummary> GetActiveSummariesForCharacter(string characterId);
    long AddStoryEventSummary(string summary, string realTime, string[] characterIds);
    void ArchiveStoryEvents(long[] eventIds, long summaryId);
    void ArchiveStoryEventSummaries(long[] summaryIds, long newSummaryId);
    RawNpcInfo? GetNpcInfo(string npcId);
    // Every NPC this playthrough has info on
    List<RawNpcInfo> GetKnownNpcs();
    long AddNpcTask(string npcId, string description, string? reward, string gameTime, double? gameSeconds = null);
    List<RawNpcTask> GetOpenNpcTasks(string npcId);
    RawNpcTask? GetOpenNpcTask(long taskId);
    // Closes an open task of that NPC; false if there is no such open task
    bool CloseNpcTask(long taskId, string npcId, NpcTaskStatus status, string gameTime, double? gameSeconds = null);
    void SaveNpcInfo(RawNpcInfo info);
    // A quest is a task with steps, in order
    void AddQuestSteps(long taskId, (string NpcId, string Goal)[] steps);
    List<RawQuestStep> GetQuestSteps(long taskId);
    bool MarkQuestStepDone(long taskId, int index, double? gameSeconds = null);
    // Open quests whose next step is the player seeing this NPC
    List<RawNpcTask> GetQuestsAwaiting(string npcId);
    // NPCs that agreed to follow the player and haven't left them since
    void AddCompanion(string npcId, string gameTime, double? gameSeconds = null);
    void RemoveCompanion(string npcId);
    string[] GetCompanions();
    // Loading an earlier save turns the game clock back: forgets the events, tasks, quest steps and
    // companions from after gameSeconds, and the summaries that took such events in. Returns how
    // many events it forgot; 0, cheaply, when nothing is later.
    int ForgetAfter(double gameSeconds);
}

public enum NpcTaskStatus { Open, Done, Cancelled }

public record RawNpcTask(long Id, string NpcId, string Description, string? Reward);
public record RawQuestStep(long TaskId, int Index, string NpcId, string Goal, bool Done);
public record RawStoryEvent(long Id, string GameTime, string RealTime, string Type, string DataJson);
public record RawStoryEventSummary(long Id, string Summary, string RealTime);
// LastCell and LastSeenGameSeconds: where and when the NPC last talked with the player
public record RawNpcInfo(string Id, string Name, string Race, string Sex,
    string? ClassName = null, string? Faction = null, string? FactionRank = null, string? Personality = null,
    string? LastCell = null, double? LastSeenGameSeconds = null);
