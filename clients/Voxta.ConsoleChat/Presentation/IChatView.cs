using Voxta.ConsoleChat.Persistence;
using Voxta.ConsoleChat.Session;
using Voxta.Model.ApiMessages.Responses;

namespace Voxta.ConsoleChat.Presentation;

public enum MainMenuChoice
{
    ContinueLastChat,
    Characters,
    Quit,
}

public record ChatChoice(bool IsNew, Guid ChatId);

public interface IChatView
{
    Task ConnectAsync(Func<IProgress<ConnectionProgress>, CancellationToken, Task> connect, CancellationToken cancellationToken);
    Task<MainMenuChoice> ShowMainMenuAsync(LastChat? lastChat, CancellationToken cancellationToken);
    Task<CharactersItemResponse?> SelectCharacterAsync(Func<CancellationToken, Task<IReadOnlyList<CharactersItemResponse>>> loadCharacters, CancellationToken cancellationToken);
    Task<ChatChoice?> SelectChatAsync(string characterName, Func<CancellationToken, Task<IReadOnlyList<ChatItemResponse>>> loadChats, CancellationToken cancellationToken);
    Task<ChatSession> StartChatAsync(Func<IProgress<LoadingUpdate>, CancellationToken, Task<ChatSession>> work, CancellationToken cancellationToken);
    Task RunChatAsync(ChatSession session, CancellationToken cancellationToken);
}
