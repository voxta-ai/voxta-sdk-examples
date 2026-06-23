using System.Text.Json;

namespace Voxta.ConsoleChat.Persistence;

public class FileLastChatStore : ILastChatStore
{
    private static readonly string FilePath = AppData.File("last-chat.json");

    public void Save(LastChat lastChat)
    {
        Directory.CreateDirectory(AppData.Directory);
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
