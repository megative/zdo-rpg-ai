namespace ZdoRpgAi.Database.Migrations.SaveGame;

// Tasks NPCs give the player. They live apart from story events, so compacting old history into
// summaries can't drop a task that is still open.
public class SaveGame002 : IMigration {
    public string GetSql() => """
        CREATE TABLE npc_task (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            npcId TEXT NOT NULL,
            description TEXT NOT NULL,
            reward TEXT,
            status TEXT NOT NULL,
            createdGameTime TEXT NOT NULL,
            closedGameTime TEXT
        );
        CREATE INDEX idx_npc_task_npc ON npc_task(npcId, status);
        """;
}
