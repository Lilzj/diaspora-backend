using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Domain.Rsvps;

/// <summary>What a page of responses is being asked for.</summary>
public sealed record RsvpQueryFilter
{
    public CommunityEventId? EventId { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public UserId? AttendeeUserId { get; init; }

    public IReadOnlyList<RsvpStatus>? Statuses { get; init; }

    /// <summary>
    /// Restricts the page to responses to events that have not finished by this moment.
    /// <para>
    /// A moment rather than a flag, because an RSVP row carries no start time and the answer has to
    /// come from the event beside it — so the caller supplies the clock rather than the repository
    /// reaching for an ambient one, and "my upcoming events" means the same thing to every caller
    /// asking in the same request.
    /// </para>
    /// </summary>
    public DateTimeOffset? UpcomingAsOf { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
