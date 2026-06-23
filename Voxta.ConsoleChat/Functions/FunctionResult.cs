namespace Voxta.ConsoleChat.Functions;

// The outcome of running a function: an optional event the AI reacts to, an optional system
// message shown locally, and whether the app should leave the chat.
public readonly record struct FunctionResult(string? EventMessage, string? SystemMessage, bool LeaveRequested)
{
    public static FunctionResult None => new(null, null, false);
    public static FunctionResult Event(string message) => new(message, null, false);
    public static FunctionResult Leave => new(null, null, true);
}
