namespace ZdoRpgAi.Database.Migrations.SaveGame;

// The game clock as a number next to what NPCs remember, so loading an earlier save can make them
// forget what happened after it (ForgetAfter). Events already stored get it from their JSON.
public class SaveGame005 : IMigration {
    public string GetSql() => """
        ALTER TABLE story_event ADD COLUMN gameSeconds REAL;
        UPDATE story_event SET gameSeconds = json_extract(dataJson, '$.gameSeconds');
        CREATE INDEX idx_story_event_seconds ON story_event(gameSeconds);
        ALTER TABLE npc_task ADD COLUMN createdGameSeconds REAL;
        ALTER TABLE npc_task ADD COLUMN closedGameSeconds REAL;
        UPDATE npc_task SET createdGameSeconds = (
            SELECT gameSeconds FROM story_event
            WHERE type = 'NpcAction' AND (
                json_extract(dataJson, '$.description') LIKE 'gives the player task #' || npc_task.id || ':%'
                OR json_extract(dataJson, '$.description') LIKE 'gives the player quest #' || npc_task.id || ' %')
            ORDER BY id LIMIT 1);
        ALTER TABLE quest_step ADD COLUMN doneGameSeconds REAL;
        ALTER TABLE companion ADD COLUMN sinceGameSeconds REAL;
        """;
}
