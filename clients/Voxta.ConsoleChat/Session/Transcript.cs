namespace Voxta.ConsoleChat.Session;

public enum TranscriptRole
{
    Character,
    User,
    System,
    Error,
    Tool,
}

// A single line in the transcript. Mutable so a streaming reply can grow in place and an
// interrupted message can be replaced with the server's truncated text.
public sealed class TranscriptEntry
{
    public Guid MessageId { get; set; }
    public required TranscriptRole Role { get; init; }
    public required string Speaker { get; init; }
    public string Text { get; set; } = "";
    public bool Pending { get; set; }
    public bool Narration { get; set; }

    // Set on a tool call entry, so the result can be written into the same line when it arrives.
    public string? ToolCallId { get; init; }

    // A continuation does not repeat the speaker name (used when a reply switches between
    // narration and dialogue mid-stream).
    public bool Continuation { get; init; }
}

// Thread-safe list of transcript entries: server threads append while the UI renders snapshots.
public sealed class Transcript
{
    private readonly List<TranscriptEntry> _entries = [];
    private readonly Lock _lock = new();

    public event Action? Changed;

    public IReadOnlyList<TranscriptEntry> Snapshot()
    {
        lock (_lock) return _entries.ToArray();
    }

    public TranscriptEntry Add(TranscriptEntry entry)
    {
        lock (_lock) _entries.Add(entry);
        Changed?.Invoke();
        return entry;
    }

    public void Remove(TranscriptEntry entry)
    {
        lock (_lock) _entries.Remove(entry);
        Changed?.Invoke();
    }

    public TranscriptEntry? LastWithMessageId(Guid messageId)
    {
        lock (_lock) return _entries.LastOrDefault(e => e.MessageId == messageId);
    }

    public TranscriptEntry? FindToolCall(string toolCallId)
    {
        lock (_lock) return _entries.LastOrDefault(e => e.ToolCallId == toolCallId);
    }

    // Notifies subscribers after an entry was mutated in place (e.g. streamed text appended).
    public void NotifyChanged() => Changed?.Invoke();
}
