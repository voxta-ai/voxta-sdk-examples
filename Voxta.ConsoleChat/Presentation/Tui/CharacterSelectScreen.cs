using Voxta.Model.ApiMessages.Responses;

namespace Voxta.ConsoleChat.Presentation.Tui;

internal static class CharacterSelectScreen
{
    public static async Task<CharactersItemResponse?> RunAsync(IReadOnlyList<CharactersItemResponse> characters, CancellationToken cancellationToken)
    {
        var options = characters
            .Select(c => string.IsNullOrEmpty(c.Version) ? c.Name : $"{c.Name} ({c.Version})")
            .ToList();

        var index = await SelectScreen.RunAsync("Select a character — Enter to start, Esc to quit", options, cancellationToken);
        return index < 0 ? null : characters[index];
    }
}
