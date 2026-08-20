using Diaspora.Contracts.Events;

namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// Converts an integration event to and from its durable form.
/// <para>
/// An abstraction rather than a static helper because the format is a deployment concern that
/// will change before the contracts do — a broker that wants CloudEvents, a payload that wants
/// compressing, a regulated deployment that wants the body encrypted at rest. Each of those is
/// a different implementation of this interface and nothing else in the platform moves.
/// </para>
/// </summary>
public interface IOutboxSerializer
{
    /// <summary>Writes an event into its durable form.</summary>
    OutboxEnvelope Serialize(
        IIntegrationEvent integrationEvent,
        IReadOnlyDictionary<string, string>? metadata = null);

    /// <summary>
    /// Reads an event back.
    /// </summary>
    /// <exception cref="UnknownIntegrationEventException">
    /// No type in this deployment implements the stored contract.
    /// </exception>
    /// <exception cref="OutboxSerializationException">
    /// The type is known but the payload could not be read into it.
    /// </exception>
    IIntegrationEvent Deserialize(OutboxEnvelope envelope);

    /// <summary>Serializes the metadata bag for storage; null when there is nothing to store.</summary>
    string? SerializeMetadata(IReadOnlyDictionary<string, string> metadata);

    /// <summary>Reads a stored metadata bag; empty when the column was null.</summary>
    IReadOnlyDictionary<string, string> DeserializeMetadata(string? metadata);
}
