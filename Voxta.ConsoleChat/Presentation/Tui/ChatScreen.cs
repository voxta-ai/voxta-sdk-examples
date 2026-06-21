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

    public static Task RunAsync(ChatSession session, CancellationToken cancellationToken)
    {
        var window = new Window { Title = $"Chatting with {session.CharacterName} — Esc or Ctrl+C to leave" };

        var lines = new ObservableCollection<string>();
        var roles = new List<ChatRole>();

        var transcript = new ListView { X = 0, Y = 0, Width = Dim.Fill(), Height = Dim.Fill(2), CanFocus = false };
        transcript.SetSource(lines);
        transcript.RowRender += (_, e) =>
        {
            if (e.Row < roles.Count)
                e.RowAttribute = new Terminal.Gui.Drawing.Attribute(ColorFor(roles[e.Row]), transcript.GetAttributeForRole(VisualRole.Normal).Background);
        };

        var status = new Label { X = 0, Y = Pos.AnchorEnd(2), Width = Dim.Fill(), Text = "" };
        var prompt = new Label { X = 0, Y = Pos.AnchorEnd(1), Text = $"{session.UserName} ▸ " };
        var input = new TextField { X = Pos.Right(prompt), Y = Pos.AnchorEnd(1), Width = Dim.Fill() };
        window.Add(transcript, status, prompt, input);

        var current = new Bubble();

        void Render()
        {
            while (lines.Count > current.Start)
            {
                lines.RemoveAt(lines.Count - 1);
                roles.RemoveAt(roles.Count - 1);
            }

            var width = Math.Max(20, transcript.Viewport.Width);
            foreach (var line in current.Wrap(width * BubbleWidthPercent / 100))
            {
                lines.Add(current.Role == ChatRole.User ? line.PadLeft(width) : line);
                roles.Add(current.Role);
            }
            transcript.MoveEnd(false);
        }

        void StartLine(ChatMessage message) => Tui.Invoke(() =>
        {
            current = new Bubble { Start = lines.Count, Role = message.Role, Speaker = message.Speaker, Text = message.Text };
            Render();
        });
        void AppendLine(string text) => Tui.Invoke(() =>
        {
            current.Text += text;
            Render();
        });
        void SetStatus(string? message) => Tui.Invoke(() => status.Text = message ?? "");
        void Leave() => Tui.Invoke(() => Tui.RequestStop(window));

        session.Interaction = new TuiInteraction(window);
        session.MessageStarted += StartLine;
        session.MessageAppended += AppendLine;
        session.StatusChanged += SetStatus;
        session.LeaveRequested += Leave;

        foreach (var message in session.History)
            StartLine(message);

        input.KeyDown += (_, key) =>
        {
            if (key == Key.Enter)
            {
                var text = input.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    session.SendText(text);
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
        Tui.Run(window);

        session.MessageStarted -= StartLine;
        session.MessageAppended -= AppendLine;
        session.StatusChanged -= SetStatus;
        session.LeaveRequested -= Leave;
        window.Dispose();

        return Task.CompletedTask;
    }

    private static Color ColorFor(ChatRole role) => role switch
    {
        ChatRole.Character => new Color(ColorName16.BrightCyan),
        ChatRole.User => new Color(ColorName16.BrightGreen),
        _ => new Color(ColorName16.Gray),
    };

    private sealed class Bubble
    {
        public int Start;
        public ChatRole Role;
        public string Speaker = "";
        public string Text = "";

        public IEnumerable<string> Wrap(int width)
        {
            width = Math.Max(8, width);
            var header = Role == ChatRole.System ? null : $"{Speaker}:";
            var body = Role == ChatRole.System ? $"[{Text}]" : Text;

            if (header != null) yield return header;

            foreach (var paragraph in body.ReplaceLineEndings("\n").Split('\n'))
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
    }
}
