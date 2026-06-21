using Voxta.ConsoleChat.Api;
using Voxta.ConsoleChat.Persistence;
using Voxta.ConsoleChat.Presentation;
using Voxta.ConsoleChat.Session;

namespace Voxta.ConsoleChat.Hosting;

public class ChatAppHost(
    VoxtaConnection connection,
    CharactersApiClient charactersApi,
    ChatsApiClient chatsApi,
    ILastChatStore lastChatStore,
    IChatView view)
{
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        await view.ConnectAsync(connection.ConnectAsync, cancellationToken);

        while (!cancellationToken.IsCancellationRequested)
        {
            var lastChat = lastChatStore.Load();
            var choice = await view.ShowMainMenuAsync(lastChat, cancellationToken);
            if (choice == MainMenuChoice.Quit) break;

            var session = choice == MainMenuChoice.ContinueLastChat && lastChat != null
                ? await view.StartChatAsync((progress, token) => connection.ResumeChatAsync(lastChat.ChatId, progress, token), cancellationToken)
                : await BrowseAndStartAsync(cancellationToken);
            if (session == null) continue;

            await view.RunChatAsync(session, cancellationToken);
            connection.LeaveChat();
        }
    }

    private async Task<ChatSession?> BrowseAndStartAsync(CancellationToken cancellationToken)
    {
        var characters = await view.RunWhileBusyAsync("Loading characters...", charactersApi.GetCharactersAsync, cancellationToken);
        var character = await view.SelectCharacterAsync(characters, cancellationToken);
        if (character == null) return null;

        var chats = await view.RunWhileBusyAsync("Loading chats...", token => chatsApi.GetChatsAsync(character.Id, token), cancellationToken);
        var choice = await view.SelectChatAsync(character.Name, chats, cancellationToken);
        if (choice == null) return null;

        return choice.IsNew
            ? await view.StartChatAsync((progress, token) => connection.StartChatAsync(character.Id, progress, token), cancellationToken)
            : await view.StartChatAsync((progress, token) => connection.ResumeChatAsync(choice.ChatId, progress, token), cancellationToken);
    }
}
