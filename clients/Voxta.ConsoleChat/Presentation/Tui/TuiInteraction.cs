using Terminal.Gui.ViewBase;
using Voxta.ConsoleChat.Functions;

namespace Voxta.ConsoleChat.Presentation.Tui;

// Lets a function (running on a background thread) ask the user for confirmation on the UI thread.
internal sealed class TuiInteraction(View owner) : IFunctionInteraction
{
    public bool Confirm(string title, string message)
    {
        var allowed = false;
        using var done = new ManualResetEventSlim();
        Tui.Invoke(() =>
        {
            try { allowed = Tui.Confirm(owner, title, message); }
            finally { done.Set(); }
        });
        done.Wait();
        return allowed;
    }
}
