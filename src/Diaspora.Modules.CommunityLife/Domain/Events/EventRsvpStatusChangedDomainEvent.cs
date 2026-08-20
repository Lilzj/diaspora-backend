using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised on every RSVP transition.
/// <para>
/// <b>This is the event the whole attendance model turns on.</b> The capacity counters are settled
/// from its handler and from nowhere else, which is what makes them reconcilable: there is exactly
/// one place to read to know how a place can move.
/// </para>
/// </summary>
public sealed record EventRsvpStatusChangedDomainEvent : DomainEvent
{
    public required EventRsvpId RsvpId { get; init; }

    public required CommunityEventId CommunityEventId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public required UserId AttendeeUserId { get; init; }

    public required RsvpStatus PreviousStatus { get; init; }

    public required RsvpStatus CurrentStatus { get; init; }

    public required int PartySize { get; init; }

    public RsvpDeclineReason DeclineReason { get; init; }

    public UserId? ChangedBy { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}
