using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Voxta.ConsoleChat.Session;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class LoadingScreen
{
    public static Task<ChatSession> RunAsync(Func<IProgress<LoadingUpdate>, CancellationToken, Task<ChatSession>> work, CancellationToken cancellationToken)
    {
        var label = new Label { X = 1, Y = 1, Width = Dim.Fill(2), Text = "Starting chat..." };
        var bar = new ProgressBar { X = 1, Y = 3, Width = Dim.Fill(2), Fraction = 0f };
        var window = new Window { Title = "Voxta Console Chat" };
        window.Add(label, bar);

        ChatSession? result = null;
        Exception? error = null;
        var progress = new LabelProgress(label, bar);

        _ = Task.Run(async () =>
        {
            try { result = await work(progress, cancellationToken); }
            catch (Exception exception) { error = exception; }
            finally { Tui.Invoke(() => Tui.RequestStop(window)); }
        }, cancellationToken);

        Tui.Run(window);
        window.Dispose();

        return error is null ? Task.FromResult(result!) : Task.FromException<ChatSession>(error);
    }

    private sealed class LabelProgress(Label label, ProgressBar bar) : IProgress<LoadingUpdate>
    {
        public void Report(LoadingUpdate value) => Tui.Invoke(() =>
        {
            if (!string.IsNullOrEmpty(value.Text)) label.Text = value.Text;
            var fraction = value.Progress > 1 ? value.Progress / 100 : value.Progress;
            bar.Fraction = (float)Math.Clamp(fraction, 0, 1);
        });
    }
}
