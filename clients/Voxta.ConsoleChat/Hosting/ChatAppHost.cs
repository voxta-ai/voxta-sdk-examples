using Voxta.Client;
using Voxta.ConsoleChat.Persistence;
using Voxta.ConsoleChat.Presentation;
using Voxta.ConsoleChat.Session;

namespace Voxta.ConsoleChat.Hosting;

public class ChatAppHost(
    VoxtaConnection connection,
    IVoxtaApiClientFactory apiClientFactory,
    VoxtaTokenProvider tokenProvider,
    ILastChatStore lastChatStore,
    IChatView view)
{
    private readonly IVoxtaApiClient _api = apiClientFactory.Create(tokenProvider);

    public async Task RunAsync(bool resumeImmediately, CancellationToken cancellationToken)
    {
        await view.ConnectAsync(connection.ConnectAsync, cancellationToken);

        var resume = resumeImmediately;
        while (!cancellationToken.IsCancellationRequested)
        {
            var lastChat = lastChatStore.Load();
            var choice = resume && lastChat != null
                ? MainMenuChoice.ContinueLastChat
                : await view.ShowMainMenuAsync(lastChat, cancellationToken);
            resume = false;
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
        var character = await view.SelectCharacterAsync(token => _api.Characters.GetCharactersAsync(cancellationToken: token), cancellationToken);
        if (character == null) return null;

        var choice = await view.SelectChatAsync(character.Name, token => _api.Chats.GetChatsAsync(character.Id, token), cancellationToken);
        if (choice == null) return null;

        return choice.IsNew
            ? await view.StartChatAsync((progress, token) => connection.StartChatAsync(character.Id, progress, token), cancellationToken)
            : await view.StartChatAsync((progress, token) => connection.ResumeChatAsync(choice.ChatId, progress, token), cancellationToken);
    }
}
