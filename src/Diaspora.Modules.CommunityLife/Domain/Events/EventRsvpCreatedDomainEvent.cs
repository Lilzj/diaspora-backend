using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>Raised the first time somebody responds to an event.</summary>
public sealed record EventRsvpCreatedDomainEvent : DomainEvent
{
    public required EventRsvpId RsvpId { get; init; }

    public required CommunityEventId CommunityEventId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public required UserId AttendeeUserId { get; init; }

    public required RsvpStatus Status { get; init; }

    public required int PartySize { get; init; }

    public required DateTimeOffset RespondedAt { get; init; }
}
