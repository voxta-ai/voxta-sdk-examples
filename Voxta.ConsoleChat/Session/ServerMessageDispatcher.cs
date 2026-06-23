using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.ConsoleChat.Session;

// Routes server messages to per-type handlers (exact type match), replacing a large switch.
public sealed class ServerMessageDispatcher
{
    private readonly Dictionary<Type, Action<ServerMessage>> _handlers = [];

    public ServerMessageDispatcher On<T>(Action<T> handler) where T : ServerMessage
    {
        _handlers[typeof(T)] = message => handler((T)message);
        return this;
    }

    public void Dispatch(ServerMessage message)
    {
        if (_handlers.TryGetValue(message.GetType(), out var handler))
            handler(message);
    }
}
