using System.Collections.ObjectModel;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Voxta.ConsoleChat.Persistence;
using Voxta.ConsoleChat.Presentation;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class MainMenuScreen
{
    public static Task<MainMenuChoice> RunAsync(LastChat? lastChat, CancellationToken cancellationToken)
    {
        var choices = new List<MainMenuChoice>();
        var labels = new ObservableCollection<string>();

        if (lastChat != null)
        {
            labels.Add($"Continue chat with {lastChat.CharacterName}");
            choices.Add(MainMenuChoice.ContinueLastChat);
        }
        labels.Add("Characters");
        choices.Add(MainMenuChoice.Characters);
        labels.Add("Quit");
        choices.Add(MainMenuChoice.Quit);

        var result = MainMenuChoice.Quit;

        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };
        list.SetSource(labels);

        var window = new Window { Title = "Voxta Console Chat — Enter to select, Esc to quit" };
        window.Add(list);

        list.KeyDown += (_, key) =>
        {
            if (key == Key.Enter && list.SelectedItem is { } index && index >= 0)
            {
                result = choices[index];
                Tui.RequestStop(window);
                key.Handled = true;
            }
            else if (Tui.IsQuit(key))
            {
                result = MainMenuChoice.Quit;
                Tui.RequestStop(window);
                key.Handled = true;
            }
        };

        list.SetFocus();
        Tui.Run(window);
        window.Dispose();

        return Task.FromResult(result);
    }
}
