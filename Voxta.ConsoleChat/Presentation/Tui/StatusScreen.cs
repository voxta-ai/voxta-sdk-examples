using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Voxta.ConsoleChat.Session;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class StatusScreen
{
    public static Task RunAsync(string message, Func<IConnectionProgress, CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        var status = new Label { X = 1, Y = 1, Width = Dim.Fill(), Text = message };
        var code = new Label { X = 1, Y = 3, Width = Dim.Fill(), Height = Dim.Fill(), Text = "" };
        var window = new Window { Title = "Voxta Console Chat" };
        window.Add(status, code);

        var progress = new LabelProgress(status, code);
        return RunCoreAsync(window, async token =>
        {
            await work(progress, token);
            return true;
        }, cancellationToken);
    }

    public static Task<T> RunAsync<T>(string message, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        var window = new Window { Title = "Voxta Console Chat" };
        window.Add(new Label { X = 1, Y = 1, Width = Dim.Fill(), Text = message });
        return RunCoreAsync(window, work, cancellationToken);
    }

    private static Task<T> RunCoreAsync<T>(Window window, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
    {
        var result = default(T)!;
        Exception? error = null;

        _ = Task.Run(async () =>
        {
            try { result = await work(cancellationToken); }
            catch (Exception exception) { error = exception; }
            finally { Tui.Invoke(() => Tui.RequestStop(window)); }
        }, cancellationToken);

        Tui.Run(window);
        window.Dispose();

        return error is null ? Task.FromResult(result) : Task.FromException<T>(error);
    }

    private sealed class LabelProgress(Label status, Label code) : IConnectionProgress
    {
        public void Status(string message) => Tui.Invoke(() => status.Text = message);

        public void DeviceCode(string userCode, string verificationUrl) =>
            Tui.Invoke(() => code.Text = $"Code: {userCode}\nURL:  {verificationUrl}");
    }
}
