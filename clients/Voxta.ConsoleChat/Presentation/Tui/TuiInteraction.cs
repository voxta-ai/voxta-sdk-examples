using Terminal.Gui.ViewBase;
using Voxta.ConsoleChat.Functions;

namespace Voxta.ConsoleChat.Presentation.Tui;

// Lets a function (running on a background thread) ask the user for confirmation on the UI thread.
internal sealed class TuiInteraction(View owner) : IFunctionInteraction
{
    public bool Confirm(string title, string message) => OnUiThread(() => Tui.Confirm(owner, title, message));

    public ToolPermission RequestToolPermission(string title, string message) =>
        OnUiThread(() => Tui.AskToolPermission(owner, title, message));

    private static T OnUiThread<T>(Func<T> query)
    {
        var result = default(T)!;
        using var done = new ManualResetEventSlim();
        Tui.Invoke(() =>
        {
            try { result = query(); }
            finally { done.Set(); }
        });
        done.Wait();
        return result;
    }
}
