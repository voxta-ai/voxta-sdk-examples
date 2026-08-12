using Voxta.ConsoleChat.Persistence;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class MainMenuScreen
{
    private static readonly string[] Banner =
    [
        "░██    ░██                          ░██",
        "░██    ░██                          ░██",
        "░██    ░██  ░███████  ░██    ░██ ░████████  ░██████",
        "░██    ░██ ░██    ░██  ░██  ░██     ░██          ░██",
        " ░██  ░██  ░██    ░██   ░█████      ░██     ░███████",
        "  ░██░██   ░██    ░██  ░██  ░██     ░██    ░██   ░██",
        "   ░███     ░███████  ░██    ░██     ░████  ░█████░██",
    ];

    public static async Task<MainMenuChoice> RunAsync(LastChat? lastChat, CancellationToken cancellationToken)
    {
        var options = new List<string>();
        var choices = new List<MainMenuChoice>();

        if (lastChat != null)
        {
            options.Add($"Continue chat with {lastChat.CharacterName}");
            choices.Add(MainMenuChoice.ContinueLastChat);
        }
        options.Add("Characters");
        choices.Add(MainMenuChoice.Characters);
        options.Add("Quit");
        choices.Add(MainMenuChoice.Quit);

        var index = await SelectScreen.RunAsync("Voxta Console Chat — Enter to select, Esc to quit", options, cancellationToken, Banner);
        return index < 0 ? MainMenuChoice.Quit : choices[index];
    }
}
