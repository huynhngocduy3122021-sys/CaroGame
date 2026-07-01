public static class GameStartSettings
{
    public enum Mode
    {
        None,
        Host,
        Join,
        Single
    }

    public static Mode StartMode = Mode.None;
}
