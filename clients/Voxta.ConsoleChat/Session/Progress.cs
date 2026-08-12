namespace Voxta.ConsoleChat.Session;

// Progress reported by the connection while connecting/authenticating, consumed via IProgress<T>.
public readonly record struct ConnectionProgress(string Status, string? DeviceCode = null, string? DeviceUrl = null);

// Progress reported by the server while a chat is loading.
public readonly record struct LoadingUpdate(double Progress, string Text);
