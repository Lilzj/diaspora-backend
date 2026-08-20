using System.Reflection;
using Diaspora.Platform.Messaging.Outbox.Model;
using Diaspora.Platform.Messaging.Outbox.Serialization;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// Builds outbox messages that look as though they came back from a claim.
/// <para>
/// <see cref="OutboxMessage.Sequence"/> is assigned by the database and has no setter, which is
/// correct — an application-assigned sequence would not be a reliable ordering source across
/// instances. A test that needs to reason about ordering still has to produce ordered messages,
/// so the builder sets it reflectively. That is a deliberate, contained cheat in a test helper
/// and not an invitation to relax the model.
/// </para>
/// </summary>
public sealed class OutboxMessageBuilder
{
    private static readonly PropertyInfo SequenceProperty =
        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.Sequence))!;

    private static readonly PropertyInfo AttemptCountProperty =
        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.AttemptCount))!;

    private static readonly JsonOutboxSerializer Serializer =
        new(new IntegrationEventTypeResolver([typeof(WidgetChangedIntegrationEvent).Assembly]));

    private long _sequence = 1;
    private string _partitionKey = "WidgetId:default";
    private int _attemptCount;
    private WidgetChangedIntegrationEvent _event = NewEvent("default");

    public static WidgetChangedIntegrationEvent NewEvent(string name) => new()
    {
        WidgetId = Guid.NewGuid(),
        Name = name,
        Count = 1,
        ChangedAt = new DateTimeOffset(2026, 7, 29, 12, 0, 0, TimeSpan.Zero)
    };

    public OutboxMessageBuilder WithSequence(long sequence)
    {
        _sequence = sequence;
        return this;
    }

    public OutboxMessageBuilder InPartition(string partitionKey)
    {
        _partitionKey = partitionKey;
        return this;
    }

    public OutboxMessageBuilder WithAttempts(int attemptCount)
    {
        _attemptCount = attemptCount;
        return this;
    }

    public OutboxMessageBuilder Carrying(WidgetChangedIntegrationEvent integrationEvent)
    {
        _event = integrationEvent;
        return this;
    }

    /// <summary>Builds a message whose payload a real serializer can read back.</summary>
    public OutboxMessage Build()
    {
        var envelope = Serializer.Serialize(_event);

        var message = OutboxMessage.Create(
            messageId: envelope.EventId,
            eventType: envelope.EventType,
            eventVersion: envelope.EventVersion,
            payload: envelope.Payload,
            metadata: null,
            module: envelope.Module,
            partitionKey: _partitionKey,
            tenantId: null,
            correlationId: "corr-test",
            causationId: null,
            occurredAt: envelope.OccurredAt,
            enqueuedAt: envelope.OccurredAt);

        SequenceProperty.SetValue(message, _sequence);
        AttemptCountProperty.SetValue(message, _attemptCount);

        return message;
    }

    /// <summary>A message whose stored payload names a contract nothing implements.</summary>
    public OutboxMessage BuildWithUnknownContract()
    {
        var message = Build();

        typeof(OutboxMessage).GetProperty(nameof(OutboxMessage.EventType))!
            .SetValue(message, "some.contract.that.left.the.building");

        return message;
    }
}
