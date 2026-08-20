using Diaspora.Contracts.Events;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// A stand-in for a real module's integration event.
/// <para>
/// Deliberately shaped like the real ones: flat primitives, a subject identifier the partition
/// convention can find, an enum-ish string and a nullable. Testing serialization against a
/// one-property record would prove nothing about the events that actually travel.
/// </para>
/// </summary>
public sealed record WidgetChangedIntegrationEvent : IntegrationEvent
{
    public required Guid WidgetId { get; init; }

    public required string Name { get; init; }

    public required int Count { get; init; }

    public string? Note { get; init; }

    public DateTimeOffset ChangedAt { get; init; }
}
