namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// A stored payload could not be turned back into an event.
/// <para>
/// Always fatal for the message that raised it: the bytes will not change on a retry. The
/// dispatcher classifies it as poison and dead-letters immediately rather than spending a
/// retry budget proving the same thing five more times.
/// </para>
/// </summary>
public sealed class OutboxSerializationException : Exception
{
    public OutboxSerializationException() { }

    public OutboxSerializationException(string message) : base(message) { }

    public OutboxSerializationException(string message, Exception innerException)
        : base(message, innerException) { }

    public OutboxSerializationException(string message, string eventType, int eventVersion, Exception innerException)
        : base(message, innerException)
    {
        EventType = eventType;
        EventVersion = eventVersion;
    }

    public string? EventType { get; }

    public int EventVersion { get; }
}
