using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when somebody is recorded as present.
/// <para>
/// Distinct from the status change that accompanies it, because a check-in carries facts a status
/// change does not — which method was used, who worked the door — and because the attendance
/// surface subscribes to this alone rather than filtering the whole RSVP stream.
/// </para>
/// </summary>
public sealed record EventCheckInCompletedDomainEvent : DomainEvent
{
    public required EventRsvpId RsvpId { get; init; }

    public required CommunityEventId CommunityEventId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public required UserId AttendeeUserId { get; init; }

    public required CheckInMethod Method { get; init; }

    /// <summary>Who worked the door. Null when the attendee confirmed their own arrival.</summary>
    public UserId? RecordedBy { get; init; }

    public required int PartySize { get; init; }

    public required DateTimeOffset CheckedInAt { get; init; }
}
