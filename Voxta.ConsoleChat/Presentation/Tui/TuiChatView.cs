using Voxta.ConsoleChat.Persistence;
using Voxta.ConsoleChat.Session;
using Voxta.Model.ApiMessages.Responses;

namespace Voxta.ConsoleChat.Presentation.Tui;

public sealed class TuiChatView : IChatView, IDisposable
{
    public TuiChatView() => Tui.Init();

    public void Dispose() => Tui.Shutdown();

    public Task ConnectAsync(Func<IProgress<ConnectionProgress>, CancellationToken, Task> connect, CancellationToken cancellationToken) =>
        StatusScreen.RunConnectAsync("Connecting to Voxta...", connect, cancellationToken);

    public Task<MainMenuChoice> ShowMainMenuAsync(LastChat? lastChat, CancellationToken cancellationToken) =>
        MainMenuScreen.RunAsync(lastChat, cancellationToken);

    public async Task<CharactersItemResponse?> SelectCharacterAsync(Func<CancellationToken, Task<IReadOnlyList<CharactersItemResponse>>> loadCharacters, CancellationToken cancellationToken)
    {
        var characters = await StatusScreen.RunBusyAsync("Loading characters...", loadCharacters, cancellationToken);
        return await CharacterSelectScreen.RunAsync(characters, cancellationToken);
    }

    public async Task<ChatChoice?> SelectChatAsync(string characterName, Func<CancellationToken, Task<IReadOnlyList<ChatItemResponse>>> loadChats, CancellationToken cancellationToken)
    {
        var chats = await StatusScreen.RunBusyAsync($"Loading chats with {characterName}...", loadChats, cancellationToken);
        return await SelectChatScreen.RunAsync(characterName, chats, cancellationToken);
    }

    public Task<ChatSession> StartChatAsync(Func<IProgress<LoadingUpdate>, CancellationToken, Task<ChatSession>> work, CancellationToken cancellationToken) =>
        LoadingScreen.RunAsync(work, cancellationToken);

    public Task RunChatAsync(ChatSession session, CancellationToken cancellationToken) =>
        ChatScreen.RunAsync(session, cancellationToken);
}
