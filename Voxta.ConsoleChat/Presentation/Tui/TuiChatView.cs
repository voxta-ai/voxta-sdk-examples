using Voxta.ConsoleChat.Persistence;
using Voxta.ConsoleChat.Session;
using Voxta.Model.ApiMessages.Responses;

namespace Voxta.ConsoleChat.Presentation.Tui;

public sealed class TuiChatView : IChatView, IDisposable
{
    public TuiChatView() => Tui.Init();

    public void Dispose() => Tui.Shutdown();

    public Task ConnectAsync(Func<IConnectionProgress, CancellationToken, Task> connect, CancellationToken cancellationToken) =>
        StatusScreen.RunAsync("Connecting to Voxta...", connect, cancellationToken);

    public Task<T> RunWhileBusyAsync<T>(string message, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken) =>
        StatusScreen.RunAsync(message, work, cancellationToken);

    public Task<MainMenuChoice> ShowMainMenuAsync(LastChat? lastChat, CancellationToken cancellationToken) =>
        MainMenuScreen.RunAsync(lastChat, cancellationToken);

    public Task<CharactersItemResponse?> SelectCharacterAsync(IReadOnlyList<CharactersItemResponse> characters, CancellationToken cancellationToken) =>
        CharacterSelectScreen.RunAsync(characters, cancellationToken);

    public Task<ChatChoice?> SelectChatAsync(string characterName, IReadOnlyList<ChatItemResponse> chats, CancellationToken cancellationToken) =>
        SelectChatScreen.RunAsync(characterName, chats, cancellationToken);

    public Task<ChatSession> StartChatAsync(Func<IProgress<LoadingUpdate>, CancellationToken, Task<ChatSession>> work, CancellationToken cancellationToken) =>
        LoadingScreen.RunAsync(work, cancellationToken);

    public Task RunChatAsync(ChatSession session, CancellationToken cancellationToken) =>
        ChatScreen.RunAsync(session, cancellationToken);
}
