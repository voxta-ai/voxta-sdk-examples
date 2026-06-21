using System.Text.Json;

namespace Voxta.ConsoleChat.Persistence;

public class FileLastChatStore : ILastChatStore
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Voxta.ConsoleChat", "last-chat.json");

    public void Save(LastChat lastChat)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(lastChat));
    }

    public LastChat? Load()
    {
        if (!File.Exists(FilePath))
            return null;
        try
        {
            return JsonSerializer.Deserialize<LastChat>(File.ReadAllText(FilePath));
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
