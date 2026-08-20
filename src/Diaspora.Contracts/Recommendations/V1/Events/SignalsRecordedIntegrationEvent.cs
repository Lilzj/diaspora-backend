using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.Recommendations.V1.Events;

/// <summary>
/// Observations were recorded for a member.
/// <para>
/// <b>A count and a set of kinds, never the observations.</b> "This member recorded twelve view
/// signals" is a fact about volume; "this member viewed these twelve things" is their browsing
/// history, and the module that exists to hold it responsibly should not be the module that
/// broadcasts it.
/// </para>
/// <para>
/// <b>Published per batch rather than per signal, and the arithmetic is the reason.</b> One home-feed
/// impression records a view for every card on the screen; an event each would put twenty outbox
/// rows on the wire for one scroll.
/// </para>
/// <para>
/// Consumers treat <c>EventId</c> as the idempotency key: delivery is at-least-once and unordered.
/// </para>
/// </summary>
public sealed record SignalsRecordedIntegrationEvent : IntegrationEvent
{
    public required Guid UserId { get; init; }

    public required int SignalCount { get; init; }

    /// <summary>The distinct kinds in the batch — <c>Viewed</c>, <c>Applied</c>, and so on.</summary>
    public required IReadOnlyList<string> Kinds { get; init; }

    /// <summary>Which module's contract they were derived from, or <c>Client</c>.</summary>
    public required string Source { get; init; }

    public required DateTimeOffset RecordedAt { get; init; }
}
