namespace ZdoRpgAi.Database.Migrations.SaveGame;

// The steps of a quest an NPC gave: a task whose steps send the player to other NPCs, the last one
// back to the giver
public class SaveGame004 : IMigration {
    public string GetSql() => """
        CREATE TABLE quest_step (
            taskId INTEGER NOT NULL,
            idx INTEGER NOT NULL,
            npcId TEXT NOT NULL,
            goal TEXT NOT NULL,
            done INTEGER NOT NULL DEFAULT 0,
            PRIMARY KEY (taskId, idx)
        );
        CREATE INDEX idx_quest_step_npc ON quest_step(npcId, done);
        """;
}
