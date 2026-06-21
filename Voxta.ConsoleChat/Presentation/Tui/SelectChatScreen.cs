using System.Collections.ObjectModel;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Voxta.ConsoleChat.Presentation;
using Voxta.Model.ApiMessages.Responses;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class SelectChatScreen
{
    public static Task<ChatChoice?> RunAsync(string characterName, IReadOnlyList<ChatItemResponse> chats, CancellationToken cancellationToken)
    {
        ChatChoice? chosen = null;

        var labels = new ObservableCollection<string> { "New Chat" };
        foreach (var chat in chats)
            labels.Add(chat.Title ?? chat.LastSession ?? chat.Created);

        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        list.SetSource(labels);

        var window = new Window { Title = $"{characterName} — Enter to select, Esc to go back" };
        window.Add(list);

        list.KeyDown += (_, key) =>
        {
            if (key == Key.Enter && list.SelectedItem is { } index && index >= 0)
            {
                chosen = index == 0 ? new ChatChoice(true, Guid.Empty) : new ChatChoice(false, chats[index - 1].Id);
                Tui.RequestStop(window);
                key.Handled = true;
            }
            else if (Tui.IsQuit(key))
            {
                Tui.RequestStop(window);
                key.Handled = true;
            }
        };

        list.SetFocus();
        Tui.Run(window);
        window.Dispose();

        return Task.FromResult(chosen);
    }
}
