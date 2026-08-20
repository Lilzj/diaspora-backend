using Diaspora.Contracts.Events;
using Diaspora.Platform.Messaging.Outbox.Abstractions;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Options;
using Diaspora.Platform.Messaging.Outbox.Partitioning;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Platform.Messaging.Outbox;

/// <summary>
/// Turns an integration event into the row that will be committed with the aggregate.
/// <para>
/// Kept out of the persistence adapter deliberately. Everything here — serialization,
/// partitioning, correlation capture, tenant stamping, the size ceiling — is platform policy
/// with no database in it, and a second storage adapter (a different provider, a test double,
/// an eventual sharded store) must not have to reimplement any of it to stay consistent.
/// </para>
/// <para>
/// <b>Correlation is captured here, at write time, and never at dispatch time.</b> The
/// dispatcher runs minutes later on a background thread with no request behind it; anything it
/// read from ambient context would belong to nobody. Capturing at enqueue is what lets an
/// operator follow one request identifier from an HTTP log, through an aggregate, into the
/// outbox row, and out into the consumer that acted on it.
/// </para>
/// </summary>
public sealed class OutboxMessageFactory
{
    private readonly IOutboxSerializer _serializer;
    private readonly IIntegrationEventTypeResolver _types;
    private readonly OutboxPartitionKeyResolver _partitions;
    private readonly IOutboxTenantResolver _tenants;
    private readonly ICorrelationContext _correlation;
    private readonly IClock _clock;
    private readonly OutboxOptions _options;

    public OutboxMessageFactory(
        IOutboxSerializer serializer,
        IIntegrationEventTypeResolver types,
        OutboxPartitionKeyResolver partitions,
        IOutboxTenantResolver tenants,
        ICorrelationContext correlation,
        IClock clock,
        IOptions<OutboxOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _serializer = serializer;
        _types = types;
        _partitions = partitions;
        _tenants = tenants;
        _correlation = correlation;
        _clock = clock;
        _options = options.Value;
    }

    public OutboxMessage Create(IIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var contract = _types.ContractFor(integrationEvent);
        var partitionKey = _partitions.Resolve(integrationEvent);
        var tenantId = _tenants.ResolveForWrite();
        var now = _clock.UtcNow;

        var metadata = BuildMetadata(contract, partitionKey, tenantId);

        var envelope = _serializer.Serialize(integrationEvent, metadata);

        GuardPayloadSize(envelope);

        return OutboxMessage.Create(
            messageId: envelope.EventId,
            eventType: envelope.EventType,
            eventVersion: envelope.EventVersion,
            payload: envelope.Payload,
            metadata: _serializer.SerializeMetadata(metadata),
            module: envelope.Module,
            partitionKey: partitionKey,
            tenantId: Clamp(tenantId, OutboxFieldLimits.TenantId),
            correlationId: Clamp(_correlation.CorrelationId, OutboxFieldLimits.CorrelationId),
            causationId: Clamp(_correlation.CausationId, OutboxFieldLimits.CausationId),
            occurredAt: envelope.OccurredAt,
            enqueuedAt: now);
    }

    private Dictionary<string, string> BuildMetadata(
        IntegrationEventContract contract, string partitionKey, string? tenantId)
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OutboxMetadataKeys.Module] = contract.Module,
            [OutboxMetadataKeys.PartitionKey] = partitionKey,
            [OutboxMetadataKeys.EventVersion] =
                contract.Version.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrWhiteSpace(_correlation.CorrelationId))
        {
            metadata[OutboxMetadataKeys.CorrelationId] = _correlation.CorrelationId;
        }

        if (!string.IsNullOrWhiteSpace(_correlation.CausationId))
        {
            metadata[OutboxMetadataKeys.CausationId] = _correlation.CausationId;
        }

        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            metadata[OutboxMetadataKeys.TenantId] = tenantId;
        }

        return metadata;
    }

    /// <summary>
    /// Refuses an oversized payload at enqueue time.
    /// <para>
    /// Thrown inside the producer's transaction, on purpose. It rolls back the aggregate along
    /// with the event, which is loud and traceable to the call site that built it. The
    /// alternative — accepting it and discovering the problem at dispatch — produces a dead
    /// letter with no stack trace, hours later, attached to a request nobody can identify.
    /// </para>
    /// <para>
    /// An integration event carrying a megabyte is nearly always a mistake: a document body or
    /// an image where a reference belongs. The same "references, not payloads" rule the media
    /// module enforces applies to messages.
    /// </para>
    /// </summary>
    private void GuardPayloadSize(OutboxEnvelope envelope)
    {
        var bytes = System.Text.Encoding.UTF8.GetByteCount(envelope.Payload);

        if (bytes > _options.MaxPayloadBytes)
        {
            throw new InvalidOperationException(
                $"The payload for '{envelope.EventType}' is {bytes:N0} bytes, above the " +
                $"{_options.MaxPayloadBytes:N0}-byte ceiling. An integration event should carry " +
                "identifiers a consumer can resolve, not the content itself.");
        }
    }

    private static string? Clamp(string? value, int limit) =>
        string.IsNullOrEmpty(value) || value.Length <= limit ? value : value[..limit];
}
