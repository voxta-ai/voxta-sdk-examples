namespace Voxta.ConsoleChat.Functions;

// What the user answered when the server asked whether a tool may run.
public enum ToolPermission
{
    Deny,
    Allow,
    AllowForSession,
}

// Lets a function, or the server, ask the user for confirmation (implemented by the UI).
public interface IFunctionInteraction
{
    bool Confirm(string title, string message);

    ToolPermission RequestToolPermission(string title, string message);
}

// Default used when no UI is attached: denies everything.
public sealed class DenyInteraction : IFunctionInteraction
{
    public static readonly DenyInteraction Instance = new();
    public bool Confirm(string title, string message) => false;
    public ToolPermission RequestToolPermission(string title, string message) => ToolPermission.Deny;
}
