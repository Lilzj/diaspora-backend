namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// The stored contract name and version match no type in this deployment.
/// <para>
/// Distinct from a serialization failure because the remedy is different and the urgency is
/// different. This is what a rolled-back deployment looks like from the outbox's side: the
/// message is fine, the code that understands it is simply not here. Dead-lettering with this
/// reason keeps the payload intact so a replay after the correct version ships succeeds.
/// </para>
/// </summary>
public sealed class UnknownIntegrationEventException : Exception
{
    public UnknownIntegrationEventException() { }

    public UnknownIntegrationEventException(string message) : base(message) { }

    public UnknownIntegrationEventException(string message, Exception innerException)
        : base(message, innerException) { }

    public UnknownIntegrationEventException(string eventType, int eventVersion)
        : base($"No integration event type in this deployment answers to contract " +
               $"'{eventType}' v{eventVersion}. The message is retained; replay it once the " +
               "assembly that declares the contract is deployed.")
    {
        EventType = eventType;
        EventVersion = eventVersion;
    }

    public string? EventType { get; }

    public int EventVersion { get; }
}
