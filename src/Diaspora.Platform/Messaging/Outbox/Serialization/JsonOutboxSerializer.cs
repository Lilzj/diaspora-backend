using System.Text.Json;
using System.Text.Json.Serialization;
using Diaspora.Contracts.Events;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Platform.Messaging.Outbox.Serialization;

/// <summary>
/// The default durable format: UTF-8 JSON, one object per event, no type discriminator inside
/// the payload.
/// <para>
/// <b>Every option below is a versioning decision.</b> The payload has to be readable by code
/// that does not exist yet and by code that already shipped, and the defaults that make a
/// serializer convenient are mostly the ones that make it brittle:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Unknown members are ignored</b> (the framework default, restated here so nobody
/// "tightens" it). A consumer running last week's build must be able to read a payload that
/// gained a field — that is what makes an additive change additive.
/// </item>
/// <item>
/// <b>Missing members are permitted.</b> A reader on a newer build sees the new property at its
/// default. Together with the rule above, this is the whole backward-compatibility story for
/// within-version evolution; anything a reader cannot default is a new version, not a new
/// field.
/// </item>
/// <item>
/// <b>Enums are written as strings.</b> Numeric enums bind a durable row to source ordering,
/// and reordering an enum is a change no reviewer treats as breaking.
/// </item>
/// <item>
/// <b>Nulls are written.</b> Omitting them saves bytes and costs the ability to tell "absent
/// because old" from "explicitly null", which is exactly the distinction a version migration
/// needs.
/// </item>
/// </list>
/// <para>
/// The event's own <c>EventId</c> and <c>OccurredAt</c> are part of the payload, so a
/// round-tripped event is equal to the original — including the identifier consumers
/// deduplicate on, which a replay must not change.
/// </para>
/// </summary>
public sealed class JsonOutboxSerializer : IOutboxSerializer
{
    private static readonly JsonSerializerOptions PayloadOptions = CreatePayloadOptions();

    private static readonly JsonSerializerOptions MetadataOptions = new(JsonSerializerDefaults.General)
    {
        WriteIndented = false
    };

    private readonly IIntegrationEventTypeResolver _types;

    public JsonOutboxSerializer(IIntegrationEventTypeResolver types)
    {
        _types = types;
    }

    public OutboxEnvelope Serialize(
        IIntegrationEvent integrationEvent,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var contract = _types.ContractFor(integrationEvent);

        string payload;

        try
        {
            // Serialized against the concrete type, not the interface. Passing IIntegrationEvent
            // would write the interface's surface — two properties — and silently discard every
            // field the event exists to carry.
            payload = JsonSerializer.Serialize(integrationEvent, contract.ClrType, PayloadOptions);
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new OutboxSerializationException(
                $"'{contract.Name}' could not be serialized. An integration event must be a " +
                "plain data contract; a payload that needs a custom converter needs it " +
                "registered here.",
                contract.Name,
                contract.Version,
                exception);
        }

        return new OutboxEnvelope
        {
            EventType = contract.Name,
            EventVersion = contract.Version,
            Payload = payload,
            EventId = integrationEvent.EventId,
            OccurredAt = integrationEvent.OccurredAt,
            Module = contract.Module,
            Metadata = metadata ?? new Dictionary<string, string>(StringComparer.Ordinal)
        };
    }

    public IIntegrationEvent Deserialize(OutboxEnvelope envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (!_types.TryResolve(envelope.EventType, envelope.EventVersion, out var contract))
        {
            throw new UnknownIntegrationEventException(envelope.EventType, envelope.EventVersion);
        }

        try
        {
            var value = JsonSerializer.Deserialize(envelope.Payload, contract.ClrType, PayloadOptions);

            if (value is not IIntegrationEvent integrationEvent)
            {
                throw new OutboxSerializationException(
                    $"'{envelope.EventType}' v{envelope.EventVersion} deserialized to " +
                    $"'{value?.GetType().FullName ?? "null"}', which is not an integration event.");
            }

            return integrationEvent;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            throw new OutboxSerializationException(
                $"The stored payload for '{envelope.EventType}' v{envelope.EventVersion} could " +
                $"not be read into '{contract.ClrType.Name}'. This is a contract change that " +
                "was not versioned.",
                envelope.EventType,
                envelope.EventVersion,
                exception);
        }
    }

    public string? SerializeMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);

        return metadata.Count == 0
            ? null
            : JsonSerializer.Serialize(metadata, MetadataOptions);
    }

    public IReadOnlyDictionary<string, string> DeserializeMetadata(string? metadata)
    {
        if (string.IsNullOrWhiteSpace(metadata))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(metadata, MetadataOptions)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            // Metadata is context, never logic. Unreadable metadata must not stop a delivery
            // that would otherwise succeed — the correlation trail is worth less than the event.
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static JsonSerializerOptions CreatePayloadOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.General)
        {
            PropertyNamingPolicy = null,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip,
            WriteIndented = false
        };

        options.Converters.Add(new JsonStringEnumConverter());

        // Strongly typed identifiers appear in the metadata of some events and in the payload of
        // any module that chooses to publish one. Registering the factory here costs nothing
        // when no event uses it and avoids a class of failure that would only be discovered by
        // the first module that did.
        options.Converters.Add(new StronglyTypedIdJsonConverterFactory());

        return options;
    }
}
