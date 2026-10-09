namespace ZdoRpgAi.Database.Migrations.SaveGame;

public static class All {
    public static IMigration[] Migrations => [
        new SaveGame001(),
        new SaveGame002(),
    ];
}
