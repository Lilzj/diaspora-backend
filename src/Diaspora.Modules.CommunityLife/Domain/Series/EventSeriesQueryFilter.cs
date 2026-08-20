using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Domain.Series;

/// <summary>
/// What a page of series is being asked for.
/// <para>
/// Deliberately much narrower than <c>EventQueryFilter</c>. A series is not a thing people browse
/// for — nobody searches for "a weekly pattern near me", they search for something that is on this
/// Saturday, and that search runs against the occurrences, which are ordinary events and already
/// carry every facet. What this filter serves is the organizer's own list of the patterns they run,
/// plus an organization's page of them. Widening it to match the event browse would build a second
/// discovery surface over rows nobody attends.
/// </para>
/// </summary>
public sealed record EventSeriesQueryFilter
{
    public OrganizationId? OrganizationId { get; init; }

    public UserId? OrganizerUserId { get; init; }

    public string? SearchTerm { get; init; }

    public IReadOnlyList<string>? CategoryKeys { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public IReadOnlyList<EventSeriesStatus>? Statuses { get; init; }

    /// <summary>The caller, so their own drafts appear in their own listing and nobody else's.</summary>
    public UserId? ViewerUserId { get; init; }

    /// <summary>
    /// Organizations the caller belongs to. Resolved once per search so a members-only series is
    /// visible to the right people without a join into the membership table on every row.
    /// </summary>
    public IReadOnlyList<OrganizationId>? ViewerOrganizationIds { get; init; }

    /// <summary>Include drafts, paused, cancelled and completed. Organizer and staff listings only.</summary>
    public bool IncludeNonPublic { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
