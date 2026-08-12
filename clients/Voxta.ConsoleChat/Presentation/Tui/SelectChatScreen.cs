using Voxta.Model.ApiMessages.Responses;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class SelectChatScreen
{
    private const int MaxChats = 20;

    public static async Task<ChatChoice?> RunAsync(string characterName, IReadOnlyList<ChatItemResponse> chats, CancellationToken cancellationToken)
    {
        var recent = chats.Take(MaxChats).ToList();
        var options = new List<string> { "+ New Chat" };
        options.AddRange(recent.Select(Describe));

        var index = await SelectScreen.RunAsync($"{characterName} — Enter to select, Esc to go back", options, cancellationToken);
        if (index < 0) return null;
        return index == 0 ? new ChatChoice(true, Guid.Empty) : new ChatChoice(false, recent[index - 1].Id);
    }

    private static string Describe(ChatItemResponse chat)
    {
        var title = string.IsNullOrWhiteSpace(chat.Title) ? "Untitled chat" : chat.Title;
        var when = string.IsNullOrWhiteSpace(chat.LastSession) ? $"created {chat.Created}" : $"last active {chat.LastSession}";
        return $"{title}  ·  {when}";
    }
}
