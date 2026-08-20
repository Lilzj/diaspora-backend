using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityLife.V1.Events;

/// <summary>
/// One date of a repeating event moved off the date its rule generated.
/// <para>
/// <b>Distinct from <see cref="EventUpdatedIntegrationEvent"/>, and the distinction is the message a
/// member ends up reading.</b> A one-off event that moves tells its attendees the date changed.
/// One week of a weekly meeting moving has to tell them <em>this week</em> changed and that the
/// pattern did not — otherwise thirty people conclude every Tuesday has shifted and stop coming on
/// the Tuesdays that did not.
/// </para>
/// <para>
/// <see cref="OriginalStartsAt"/> is the slot the rule generated and never moves, however many times
/// the occurrence does. It is the date the series holds an exception for, and the one a consumer
/// holding a stale reminder was scheduled against.
/// </para>
/// </summary>
public sealed record EventOccurrenceRescheduledIntegrationEvent : IntegrationEvent
{
    public required Guid CommunityEventId { get; init; }

    public required Guid SeriesId { get; init; }

    public Guid? OrganizationId { get; init; }

    /// <summary>Its ordinal in the series. Stable: cancelling one date does not renumber the rest.</summary>
    public required int Sequence { get; init; }

    public required string Title { get; init; }

    public required string Slug { get; init; }

    /// <summary>The instant the rule generated. Never moves. See the type summary.</summary>
    public required DateTimeOffset OriginalStartsAt { get; init; }

    public required DateTimeOffset PreviousStartsAt { get; init; }

    public required DateTimeOffset CurrentStartsAt { get; init; }

    public required DateTimeOffset CurrentEndsAt { get; init; }

    public required string TimeZoneId { get; init; }

    /// <summary>How many people were holding a place when it moved.</summary>
    public required int AffectedAttendeeCount { get; init; }

    public Guid? ChangedByUserId { get; init; }

    public Guid? OrganizerUserId { get; init; }

    public required DateTimeOffset RescheduledAt { get; init; }
}
