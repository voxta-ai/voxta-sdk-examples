namespace Voxta.ConsoleChat.Functions;

// Lets a function ask the user for confirmation (implemented by the UI).
public interface IFunctionInteraction
{
    bool Confirm(string title, string message);
}

// Default used when no UI is attached: denies everything.
public sealed class DenyInteraction : IFunctionInteraction
{
    public static readonly DenyInteraction Instance = new();
    public bool Confirm(string title, string message) => false;
}
