using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when one dated instance of a series moves off the date the rule generated for it.
/// <para>
/// <b>Distinct from an ordinary reschedule, and the distinction is the whole reason the frontend
/// asked for it.</b> Moving a one-off event tells its attendees a date changed. Moving one week of a
/// weekly meeting has to tell them that <em>this week</em> changed and that the pattern did not,
/// because the alternative is thirty people assuming every Tuesday has moved.
/// </para>
/// <para>
/// <see cref="OriginalStartsAt"/> is the date the rule generated, not the previous start: an
/// occurrence moved twice still came from one slot, and that slot is the one the rule holds an
/// exception for.
/// </para>
/// </summary>
public sealed record EventOccurrenceRescheduledDomainEvent : DomainEvent
{
    public required CommunityEventId CommunityEventId { get; init; }

    public required EventSeriesId EventSeriesId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    /// <summary>The instant the rule generated. Never moves, however often the occurrence does.</summary>
    public required DateTimeOffset OriginalStartsAt { get; init; }

    public required DateTimeOffset PreviousStartsAt { get; init; }

    public required DateTimeOffset CurrentStartsAt { get; init; }

    /// <summary>How many people were holding a place when it moved.</summary>
    public required int AffectedAttendeeCount { get; init; }

    public UserId? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
