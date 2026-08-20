namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// The wire form of an integration event: what is written to the outbox and read back.
/// <para>
/// Separated from <c>OutboxMessage</c> because the two answer different questions. The message
/// is a delivery record — leases, attempts, backoff — and its shape belongs to the dispatcher.
/// The envelope is the contract between a producer and a consumer separated by time, possibly
/// by a deployment, and eventually by a network. Only the envelope's shape is frozen.
/// </para>
/// </summary>
public sealed record OutboxEnvelope
{
    /// <summary>The stable contract name. See <see cref="IntegrationEventContract"/>.</summary>
    public required string EventType { get; init; }

    /// <summary>The contract version this payload was written against.</summary>
    public required int EventVersion { get; init; }

    /// <summary>The serialized event body.</summary>
    public required string Payload { get; init; }

    /// <summary>The event's own identifier, and the consumer's idempotency key.</summary>
    public required Guid EventId { get; init; }

    /// <summary>When the fact the event describes actually happened.</summary>
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>The module that declared the contract.</summary>
    public required string Module { get; init; }

    /// <summary>
    /// Everything that travels beside the payload without being part of it: correlation,
    /// causation, tenant, and whatever a future transport needs to add.
    /// <para>
    /// A bag rather than fixed properties because the alternative is a schema change — and a
    /// migration over every historic row — the first time a transport needs one more header.
    /// </para>
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);
}
