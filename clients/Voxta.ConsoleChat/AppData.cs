namespace Voxta.ConsoleChat;

// Per-user local folder for this app's persisted files.
internal static class AppData
{
    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Voxta.ConsoleChat");

    public static string File(string name) => Path.Combine(Directory, name);
}
