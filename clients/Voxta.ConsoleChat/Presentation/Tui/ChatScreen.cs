using System.Collections.ObjectModel;
using System.Text;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;
using Voxta.ConsoleChat.Session;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class ChatScreen
{
    private const int BubbleWidthPercent = 75;
    private const string SpinnerFrames = "|/-\\";

    public static Task RunAsync(ChatSession session, CancellationToken cancellationToken)
    {
        var window = new Window { Title = $"Chatting with {session.CharacterName} — Esc or Ctrl+C to leave" };

        var lines = new ObservableCollection<string>();
        var roles = new List<TranscriptRole>();
        var styles = new List<TextStyle>();
        var spinner = 0;
        TranscriptEntry? revealingEntry = null; // the character line currently being typed out
        var revealed = 0;                       // how many of its characters are shown so far

        var transcript = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(2), CanFocus = false };
        transcript.SetSource(lines);
        transcript.RowRender += (_, e) =>
        {
            if (e.Row >= roles.Count) return;
            var background = transcript.GetAttributeForRole(VisualRole.Normal).Background;
            e.RowAttribute = new Terminal.Gui.Drawing.Attribute(ColorFor(roles[e.Row]), background, styles[e.Row]);
        };

        var status = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill(), Text = "" };
        var prompt = new Label { X = 0, Y = Pos.AnchorEnd(1), Text = $"{session.UserName} ▸ " };
        var input = new TextField { X = Pos.Right(prompt), Y = Pos.AnchorEnd(1), Width = Dim.Fill() };
        window.Add(transcript, status, prompt, input);

        var typing = false;
        input.ValueChanged += (_, _) =>
        {
            var hasText = !string.IsNullOrEmpty(input.Text);
            if (hasText == typing) return;
            typing = hasText;
            if (hasText) session.TypingStarted();
            else session.TypingStopped(sent: false);
        };

        string VisibleText(TranscriptEntry entry) =>
            ReferenceEquals(entry, revealingEntry)
                ? entry.Text[..Math.Min(revealed, entry.Text.Length)]
                : entry.Text;

        void Render()
        {
            roles.Clear();
            styles.Clear();
            lines.Clear();
            var width = Math.Max(20, transcript.Viewport.Width);
            var frame = SpinnerFrames[spinner % SpinnerFrames.Length];
            foreach (var entry in session.Transcript.Snapshot())
            foreach (var (text, style) in WrapEntry(entry, VisibleText(entry), width, frame))
            {
                lines.Add(entry.Role == TranscriptRole.User ? text.PadLeft(width) : text);
                roles.Add(entry.Role);
                styles.Add(style);
            }
            if (lines.Count > 0) transcript.MoveEnd(false);
        }

        var background = transcript.GetAttributeForRole(VisualRole.Normal).Background;
        var yourTurn = new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(ColorName16.BrightGreen), background));
        var notYourTurn = new Scheme(new Terminal.Gui.Drawing.Attribute(new Color(ColorName16.Gray), background));
        prompt.SetScheme(notYourTurn);

        var baseTitle = window.Title;
        void OnChanged() => Tui.Invoke(Render);
        void OnStatus(string? message) => Tui.Invoke(() => status.Text = message ?? "");
        void OnConnection(bool connected) => Tui.Invoke(() =>
            window.Title = connected ? baseTitle : $"{baseTitle}  —  ⚠ DISCONNECTED (reconnecting…)");
        void OnTurn(bool userTurn) => Tui.Invoke(() => prompt.SetScheme(userTurn ? yourTurn : notYourTurn));
        void OnLeave() => Tui.Invoke(() => Tui.RequestStop(window));

        session.Interaction = new TuiInteraction(window);
        session.Transcript.Changed += OnChanged;
        session.StatusChanged += OnStatus;
        session.ConnectionChanged += OnConnection;
        session.TurnChanged += OnTurn;
        session.LeaveRequested += OnLeave;

        // Existing history (and the user's own lines) appears at once; only new replies type out.
        var initial = session.Transcript.Snapshot().LastOrDefault();
        revealingEntry = initial;
        revealed = initial?.Text.Length ?? 0;

        bool Animate()
        {
            var snapshot = session.Transcript.Snapshot();
            var last = snapshot.Count > 0 ? snapshot[^1] : null;
            var changed = false;

            if (!ReferenceEquals(last, revealingEntry))
            {
                revealingEntry = last;
                revealed = last is { Role: TranscriptRole.Character } ? 0 : last?.Text.Length ?? 0;
                changed = true;
            }

            if (revealingEntry is { Role: TranscriptRole.Character } && revealed < revealingEntry.Text.Length)
            {
                // Reveal a random few characters (sometimes none) so it reads like irregular typing.
                revealed = Math.Min(revealingEntry.Text.Length, revealed + Random.Shared.Next(0, 5));
                changed = true;
            }

            if (snapshot.Any(e => e.Pending))
            {
                spinner++;
                changed = true;
            }

            return changed;
        }

        var animationTimer = Tui.AddTimeout(TimeSpan.FromMilliseconds(30), () =>
        {
            if (Animate()) Render();
            return true;
        });

        input.KeyDown += (_, key) =>
        {
            if (key == Key.Enter)
            {
                var text = input.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    session.SendText(text);
                    if (typing) { typing = false; session.TypingStopped(sent: true); }
                    input.Text = "";
                }
                key.Handled = true;
            }
            else if (key == Key.Esc)
            {
                if (Tui.Confirm(window, "Leave chat", $"Leave your chat with {session.CharacterName}?"))
                    Tui.RequestStop(window);
                key.Handled = true;
            }
            else if (key == Key.C.WithCtrl)
            {
                Tui.RequestStop(window);
                key.Handled = true;
            }
        };

        input.SetFocus();
        Render();
        Tui.AddTimeout(TimeSpan.FromMilliseconds(50), () =>
        {
            Render(); // re-wrap once the real viewport width is known after layout
            return false;
        });
        Tui.Run(window);

        Tui.RemoveTimeout(animationTimer);
        session.Transcript.Changed -= OnChanged;
        session.StatusChanged -= OnStatus;
        session.ConnectionChanged -= OnConnection;
        session.TurnChanged -= OnTurn;
        session.LeaveRequested -= OnLeave;
        window.Dispose();

        return Task.CompletedTask;
    }

    // Yields (text, style) lines: a bold speaker header (unless a continuation), then the body
    // (italic for narration).
    private static IEnumerable<(string Text, TextStyle Style)> WrapEntry(TranscriptEntry entry, string visibleText, int width, char spinnerFrame)
    {
        var bubble = Math.Max(8, width * BubbleWidthPercent / 100);

        if (entry.Role is TranscriptRole.System or TranscriptRole.Error or TranscriptRole.Tool)
        {
            foreach (var line in Wrap(visibleText, bubble))
                yield return (line, TextStyle.None);
            yield break;
        }

        if (!entry.Continuation)
            yield return (entry.Speaker, TextStyle.Bold);

        var body = entry.Pending ? spinnerFrame.ToString() : visibleText;
        var bodyStyle = entry.Narration ? TextStyle.Italic : TextStyle.None;
        foreach (var line in Wrap(body, bubble))
            yield return (line, bodyStyle);
    }

    private static IEnumerable<string> Wrap(string text, int width)
    {
        foreach (var paragraph in text.ReplaceLineEndings("\n").Split('\n'))
        {
            var line = new StringBuilder();
            foreach (var word in paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (line.Length > 0 && line.Length + 1 + word.Length > width)
                {
                    yield return line.ToString();
                    line.Clear();
                }
                if (line.Length > 0) line.Append(' ');
                line.Append(word);
            }
            yield return line.ToString();
        }
    }

    private static Color ColorFor(TranscriptRole role) => role switch
    {
        TranscriptRole.Character => new Color(ColorName16.BrightCyan),
        TranscriptRole.User => new Color(ColorName16.BrightGreen),
        TranscriptRole.Error => new Color(ColorName16.BrightRed),
        TranscriptRole.Tool => new Color(ColorName16.BrightYellow),
        _ => new Color(ColorName16.Gray),
    };
}
