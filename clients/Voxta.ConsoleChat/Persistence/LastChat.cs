namespace Voxta.ConsoleChat.Persistence;

public record LastChat(Guid ChatId, Guid CharacterId, string CharacterName);

public interface ILastChatStore
{
    void Save(LastChat lastChat);
    LastChat? Load();
}
