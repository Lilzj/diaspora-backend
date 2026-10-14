using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Domain.CommunityEvents;

/// <summary>
/// What a page of events is being asked for.
/// <para>
/// This record is the module's search contract in miniature: every facet the brief names —
/// keyword, organization, country, city, neighbourhood, type, date range, free, family friendly,
/// student, religious, business, cultural, nearby, featured — is a property here, and the shipped
/// EF engine and any future OpenSearch adapter both answer the same shape. Adding a facet is a
/// property here and a clause in each engine, never a new method.
/// </para>
/// </summary>
public sealed record EventQueryFilter
{
    public string? SearchTerm { get; init; }

    public IReadOnlyList<string>? CategoryKeys { get; init; }

    public OrganizationId? OrganizationId { get; init; }

    public UserId? OrganizerUserId { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>B4's materialised path. The subtree scope — everything under London.</summary>
    public string? AreaPathPrefix { get; init; }

    /// <summary>
    /// A map viewport, as four edges, matched against the venue's own coordinate.
    /// <para>
    /// An event is at a place somebody can stand, and its venue coordinate is published as-is —
    /// there is no disclosure rule to honour here, unlike a housing listing, because a venue is
    /// where the public is being invited to turn up.
    /// </para>
    /// </summary>
    public double? BoundsSouth { get; init; }

    public double? BoundsNorth { get; init; }

    public double? BoundsWest { get; init; }

    public double? BoundsEast { get; init; }

    /// <summary>
    /// With a rectangle: a town whose events with no venue coordinate are kept, compared
    /// case-insensitively. A circle search that also named a town still finds them (14 Oct 2026).
    /// </summary>
    public string? UnpinnedCityName { get; init; }

    /// <summary>Whether a viewport was supplied at all.</summary>
    public bool HasBounds =>
        BoundsSouth is not null && BoundsNorth is not null
        && BoundsWest is not null && BoundsEast is not null;

    /// <summary>
    /// Whether the rectangle wraps across ±180°, which it does exactly when its western edge is
    /// numerically east of its eastern edge. Without the branch the predicate is unsatisfiable.
    /// </summary>
    public bool BoundsCrossAntimeridian => HasBounds && BoundsWest > BoundsEast;

    public DateTimeOffset? StartsAfter { get; init; }

    public DateTimeOffset? StartsBefore { get; init; }

    public bool? FreeOnly { get; init; }

    public bool? FamilyFriendlyOnly { get; init; }

    public bool? ChildFriendlyOnly { get; init; }

    public bool? StudentOnly { get; init; }

    public bool? FeaturedOnly { get; init; }

    public bool? VerifiedOrganizersOnly { get; init; }

    public EventAttendanceMode? AttendanceMode { get; init; }

    public IReadOnlyList<EventStatus>? Statuses { get; init; }

    /// <summary>
    /// Only events that still have room. What a member filtering a popular festival weekend wants,
    /// and what a waiting list makes possible to answer honestly.
    /// </summary>
    public bool? HasPlacesRemaining { get; init; }

    /// <summary>The caller, so their own drafts appear in their own listings and nobody else's.</summary>
    public UserId? ViewerUserId { get; init; }

    /// <summary>
    /// Organizations the caller belongs to. What makes a members-only event visible to the right
    /// people without a join into the membership table on every row.
    /// </summary>
    public IReadOnlyList<OrganizationId>? ViewerOrganizationIds { get; init; }

    /// <summary>Include drafts, cancelled and archived events. Staff and organizer listings only.</summary>
    public bool IncludeNonPublic { get; init; }

    /// <summary>Include events that have already finished. Off by default: an event list is a plan.</summary>
    public bool IncludePast { get; init; }

    public EventSortOrder SortOrder { get; init; } = EventSortOrder.Soonest;

    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
