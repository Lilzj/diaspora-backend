using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityLife.Domain.Events;

/// <summary>
/// Raised when an event is called off.
/// <para>
/// Its own event rather than a status change with a particular destination, because it is the one
/// transition with a consequence for other people: every RSVP has to be released and everybody who
/// was coming has to be told. A consumer subscribing to status changes and filtering for cancelled
/// would have to know that rule; a consumer subscribing to this does not.
/// </para>
/// </summary>
public sealed record CommunityEventCancelledDomainEvent : DomainEvent
{
    public required CommunityEventId CommunityEventId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public required string Title { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    /// <summary>How many people were holding a place. What a consumer sizes the notification by.</summary>
    public required int AffectedAttendeeCount { get; init; }

    public UserId? CancelledBy { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset CancelledAt { get; init; }
}
