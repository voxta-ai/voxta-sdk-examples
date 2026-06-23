using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Voxta.ConsoleChat.Session;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class StatusScreen
{
    // Shows connection progress (status + device code) while connecting/authenticating.
    public static Task RunConnectAsync(string message, Func<IProgress<ConnectionProgress>, CancellationToken, Task> work, CancellationToken cancellationToken)
    {
        var status = new Label { X = 1, Y = 1, Width = Dim.Fill(), Text = message };
        var code = new Label { X = 1, Y = 3, Width = Dim.Fill(), Height = Dim.Fill(), Text = "" };
        var window = new Window { Title = "Voxta Console Chat" };
        window.Add(status, code);

        var progress = new ConnectProgress(status, code);
        return RunCoreAsync(window, async token =>
        {
            await work(progress, token);
            return true;
        }, cancellationToken);
    }

    // Shows a message while a piece of work runs, returning its result.
    public static Task<T> RunBusyAsync<T>(string message, Func<CancellationToken, Task<T>> work, CancellationToken cancellationToken)
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

    private sealed class ConnectProgress(Label status, Label code) : IProgress<ConnectionProgress>
    {
        public void Report(ConnectionProgress value) => Tui.Invoke(() =>
        {
            status.Text = value.Status;
            if (value.DeviceCode != null)
                code.Text = $"Code: {value.DeviceCode}\nURL:  {value.DeviceUrl}";
        });
    }
}
