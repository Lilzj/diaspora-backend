using System.Text.Json;
using Diaspora.Platform.Messaging.Outbox.Serialization;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// The durable format, and the versioning promises made through it.
/// <para>
/// Everything here is a compatibility test disguised as a serialization test. The payload has to
/// be readable by code that has not been written and by code that shipped last month, and the
/// failures these guard against all look identical in production: a dead letter with a
/// deserialization error, hours after the deployment that caused it.
/// </para>
/// </summary>
public sealed class JsonOutboxSerializerTests
{
    private static JsonOutboxSerializer Serializer() =>
        new(new IntegrationEventTypeResolver([typeof(WidgetChangedIntegrationEvent).Assembly]));

    private static WidgetChangedIntegrationEvent SampleEvent() => new()
    {
        WidgetId = Guid.Parse("8f6d1e0e-3b2a-4f8b-9c7d-0f1a2b3c4d5e"),
        Name = "A widget",
        Count = 42,
        Note = null,
        ChangedAt = new DateTimeOffset(2026, 7, 29, 10, 0, 0, TimeSpan.Zero)
    };

    [Fact]
    public void An_event_round_trips_with_every_field_intact()
    {
        var serializer = Serializer();
        var original = SampleEvent();

        var restored = (WidgetChangedIntegrationEvent)serializer.Deserialize(serializer.Serialize(original));

        restored.Should().BeEquivalentTo(original);
    }

    /// <summary>
    /// The identifier consumers deduplicate on must survive the round trip. If it did not, a
    /// replay would look like a new event to every consumer that followed the guidance.
    /// </summary>
    [Fact]
    public void The_event_identifier_survives_serialization()
    {
        var serializer = Serializer();
        var original = SampleEvent();

        var envelope = serializer.Serialize(original);
        var restored = serializer.Deserialize(envelope);

        envelope.EventId.Should().Be(original.EventId);
        restored.EventId.Should().Be(original.EventId);
        restored.OccurredAt.Should().Be(original.OccurredAt);
    }

    /// <summary>
    /// The additive-change guarantee, from the reader's side: a consumer running older code
    /// meets a payload that gained a field and reads it anyway.
    /// </summary>
    [Fact]
    public void A_payload_carrying_an_unknown_field_still_deserializes()
    {
        var serializer = Serializer();
        var envelope = serializer.Serialize(SampleEvent());

        var widened = JsonDocument.Parse(envelope.Payload).RootElement.GetRawText()
            .TrimEnd('}') + ",\"FieldFromTheFuture\":\"added in v2\"}";

        var restored = (WidgetChangedIntegrationEvent)serializer.Deserialize(envelope with { Payload = widened });

        restored.Name.Should().Be("A widget");
        restored.Count.Should().Be(42);
    }

    /// <summary>
    /// The same guarantee from the other side: a reader on newer code meets a payload written
    /// before a field existed, and gets the default rather than an exception.
    /// </summary>
    [Fact]
    public void A_payload_missing_an_optional_field_still_deserializes()
    {
        var serializer = Serializer();
        var envelope = serializer.Serialize(SampleEvent());

        using var document = JsonDocument.Parse(envelope.Payload);

        var narrowed = "{" + string.Join(
            ",",
            document.RootElement.EnumerateObject()
                .Where(property => property.Name != "Note")
                .Select(property => $"\"{property.Name}\":{property.Value.GetRawText()}")) + "}";

        var restored = (WidgetChangedIntegrationEvent)serializer.Deserialize(envelope with { Payload = narrowed });

        restored.Note.Should().BeNull();
    }

    /// <summary>
    /// A rolling deployment, from the old instance's side. The message is fine; the code that
    /// understands it is not here yet.
    /// </summary>
    [Fact]
    public void An_unknown_contract_raises_the_distinguishable_unknown_event_failure()
    {
        var serializer = Serializer();
        var envelope = serializer.Serialize(SampleEvent());

        var act = () => serializer.Deserialize(envelope with { EventType = "some.future.contract" });

        act.Should().Throw<UnknownIntegrationEventException>()
            .Which.EventType.Should().Be("some.future.contract");
    }

    /// <summary>
    /// A known contract whose payload cannot be read is a contract change somebody shipped
    /// without versioning it. Distinct exception, because the remedy is entirely different.
    /// </summary>
    [Fact]
    public void An_unreadable_payload_raises_a_serialization_failure()
    {
        var serializer = Serializer();
        var envelope = serializer.Serialize(SampleEvent());

        var act = () => serializer.Deserialize(envelope with { Payload = "{ not json at all" });

        act.Should().Throw<OutboxSerializationException>();
    }

    /// <summary>
    /// Numeric enums bind a durable row to source ordering, and reordering an enum is a change
    /// nobody reviews as breaking. The payload must not contain bare integers where a name
    /// belongs.
    /// </summary>
    [Fact]
    public void The_payload_carries_the_events_own_identifier_by_name()
    {
        var envelope = Serializer().Serialize(SampleEvent());

        envelope.Payload.Should().Contain("\"WidgetId\"");
        envelope.Payload.Should().Contain("8f6d1e0e-3b2a-4f8b-9c7d-0f1a2b3c4d5e");
    }

    /// <summary>
    /// A null must be written rather than omitted, so "absent because the payload is old" stays
    /// distinguishable from "explicitly nothing" — the distinction a version migration needs.
    /// </summary>
    [Fact]
    public void A_null_field_is_written_rather_than_omitted()
    {
        var envelope = Serializer().Serialize(SampleEvent() with { Note = null });

        envelope.Payload.Should().Contain("\"Note\":null");
    }

    [Fact]
    public void Metadata_round_trips()
    {
        var serializer = Serializer();

        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OutboxMetadataKeys.CorrelationId] = "abc-123",
            [OutboxMetadataKeys.Module] = "Providers"
        };

        var restored = serializer.DeserializeMetadata(serializer.SerializeMetadata(metadata));

        restored.Should().BeEquivalentTo(metadata);
    }

    [Fact]
    public void An_empty_metadata_bag_is_stored_as_null_rather_than_an_empty_object()
    {
        Serializer().SerializeMetadata(
            new Dictionary<string, string>(StringComparer.Ordinal)).Should().BeNull();
    }

    /// <summary>
    /// Metadata is context, never logic. Unreadable metadata must not stop a delivery that would
    /// otherwise succeed — losing the correlation trail costs less than losing the event.
    /// </summary>
    [Fact]
    public void Unreadable_metadata_degrades_to_empty_rather_than_failing_the_delivery()
    {
        Serializer().DeserializeMetadata("{ corrupted").Should().BeEmpty();
    }

    /// <summary>
    /// The module travels on the envelope, so per-module statistics work without the dispatcher
    /// having to know anything about who produced a message.
    /// </summary>
    [Fact]
    public void The_envelope_records_the_owning_module()
    {
        // Derived from the namespace: this event lives outside Diaspora.Contracts, so the
        // convention falls back to the last namespace segment.
        Serializer().Serialize(SampleEvent()).Module.Should().Be("Outbox");
    }
}
