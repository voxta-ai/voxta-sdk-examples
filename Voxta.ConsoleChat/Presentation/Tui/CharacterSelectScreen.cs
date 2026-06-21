using System.Collections.ObjectModel;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Voxta.Model.ApiMessages.Responses;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class CharacterSelectScreen
{
    public static Task<CharactersItemResponse?> RunAsync(IReadOnlyList<CharactersItemResponse> characters, CancellationToken cancellationToken)
    {
        CharactersItemResponse? chosen = null;

        var labels = new ObservableCollection<string>(
            characters.Select(c => string.IsNullOrEmpty(c.Version) ? c.Name : $"{c.Name} ({c.Version})"));

        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        list.SetSource(labels);

        var window = new Window { Title = "Select a character — Enter to start, Esc to quit" };
        window.Add(list);

        list.KeyDown += (_, key) =>
        {
            if (key == Key.Enter && list.SelectedItem is { } index && index >= 0)
            {
                chosen = characters[index];
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
