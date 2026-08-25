// Terminal.Gui 2.4 marks the static Application entry points obsolete ahead of an
// instance-based model that is not yet publicly constructible (ApplicationImpl is internal).
// The static API remains the supported way to run an app, so the suppression is isolated here.
#pragma warning disable CS0618
using Terminal.Gui.App;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Voxta.ConsoleChat.Functions;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class Tui
{
    public static void Init() => Application.Init();

    public static void Shutdown() => Application.Shutdown();

    public static void Run(Window window) => Application.Run(window);

    public static void RequestStop(Window window) => Application.RequestStop(window);

    public static void Invoke(Action action) => Application.Invoke(action);

    public static object? AddTimeout(TimeSpan interval, Func<bool> callback) => Application.AddTimeout(interval, callback);

    public static void RemoveTimeout(object? token)
    {
        if (token != null) Application.RemoveTimeout(token);
    }

    public static bool Confirm(View owner, string title, string message) =>
        MessageBox.Query(owner.App!, title, message, "Yes", "No") == 0;

    public static ToolPermission AskToolPermission(View owner, string title, string message) =>
        MessageBox.Query(owner.App!, title, message, "Allow once", "Allow for this chat", "Deny") switch
        {
            0 => ToolPermission.Allow,
            1 => ToolPermission.AllowForSession,
            _ => ToolPermission.Deny,
        };

    public static bool IsQuit(Key key) => key == Key.Esc || key == Key.C.WithCtrl;
}
