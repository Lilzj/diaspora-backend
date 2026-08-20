using Diaspora.Contracts.Events;

namespace Diaspora.UnitTests.Platform.Outbox;

/// <summary>
/// An event the partition convention cannot classify: no explicit key, and no property whose
/// name matches any subject noun in its own type name.
/// <para>
/// Exists to pin the fallback. The tempting default — group these by event type — would make
/// every unclassifiable event in the platform queue behind every other one of its kind, and it
/// would look like it was working right up until one of them failed.
/// </para>
/// </summary>
public sealed record OutboxUnpartitionableEvent : IntegrationEvent
{
    public required string Description { get; init; }
}
