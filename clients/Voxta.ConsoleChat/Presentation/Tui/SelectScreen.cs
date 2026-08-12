using System.Collections.ObjectModel;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Voxta.ConsoleChat.Presentation.Tui;

// A list the user picks from with the keyboard. Returns the selected index, or -1 if cancelled.
internal static class SelectScreen
{
    public static Task<int> RunAsync(string title, IReadOnlyList<string> options, CancellationToken cancellationToken, IReadOnlyList<string>? banner = null)
    {
        var chosen = -1;
        var window = new Window { Title = title };
        var list = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill() };

        if (banner != null)
        {
            // Centered as a block (each line left-aligned within a fixed-width, centered label).
            var header = new Label
            {
                Text = string.Join('\n', banner),
                X = Pos.Center(),
                Y = 1,
                Width = banner.Max(line => line.Length),
                Height = banner.Count,
            };
            list.Y = Pos.Bottom(header) + 1;
            window.Add(header);
        }

        var labels = new ObservableCollection<string>(options);
        list.SetSource(labels);
        if (labels.Count > 0) list.SelectedItem = 0;
        window.Add(list);

        list.KeyDown += (_, key) =>
        {
            if (key == Key.Enter && list.SelectedItem is { } index && index >= 0)
                chosen = index;
            else if (!Tui.IsQuit(key))
                return;
            Tui.RequestStop(window);
            key.Handled = true;
        };

        list.SetFocus();
        Tui.Run(window);
        window.Dispose();

        return Task.FromResult(chosen);
    }
}
