namespace ZdoRpgAi.Database.Migrations.SaveGame;

// NPCs that follow the player because they agreed to, so they talk on the road after a restart too
public class SaveGame003 : IMigration {
    public string GetSql() => """
        CREATE TABLE companion (
            npcId TEXT PRIMARY KEY,
            sinceGameTime TEXT NOT NULL
        );
        """;
}
