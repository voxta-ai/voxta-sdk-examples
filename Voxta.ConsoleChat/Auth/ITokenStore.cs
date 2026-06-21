namespace Voxta.ConsoleChat.Auth;

public interface ITokenStore
{
    void Save(string token);
    string? Load();
}
