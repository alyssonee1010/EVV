// Carries the level id chosen in the Main Menu's Stage Select panel across the scene load into
// the gameplay scene, since all levels share a single gameplay scene rather than one scene each.
public static class EVVPendingLevelSelection
{
    public static string LevelId { get; private set; }

    public static void Set(string levelId)
    {
        LevelId = levelId;
    }

    public static string Consume()
    {
        string id = LevelId;
        LevelId = null;
        return id;
    }
}
