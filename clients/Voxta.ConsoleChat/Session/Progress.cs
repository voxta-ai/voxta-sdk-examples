namespace Voxta.ConsoleChat.Session;

// Progress reported by the connection while connecting/authenticating, consumed via IProgress<T>.
// BindingMessage: when Voxta prompted its signed-in users, the number they pick to approve.
public readonly record struct ConnectionProgress(string Status, string? DeviceCode = null, string? DeviceUrl = null, string? BindingMessage = null);

// Progress reported by the server while a chat is loading.
public readonly record struct LoadingUpdate(double Progress, string Text);
