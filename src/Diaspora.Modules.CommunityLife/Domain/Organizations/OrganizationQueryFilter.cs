using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.CommunityLife.Domain.Organizations;

/// <summary>
/// What a page of organizations is being asked for.
/// <para>
/// A record rather than a wall of optional repository parameters, for the reason B7 first recorded:
/// a method with fourteen nullable arguments is one every caller passes nulls into positionally,
/// and the first one that transposes two of them fails silently.
/// </para>
/// </summary>
public sealed record OrganizationQueryFilter
{
    public string? SearchTerm { get; init; }

    public IReadOnlyList<string>? TypeKeys { get; init; }

    public string? CountryCode { get; init; }

    public string? CityName { get; init; }

    public Guid? NeighborhoodAreaId { get; init; }

    /// <summary>B4's materialised path. The subtree scope — everything under London.</summary>
    public string? AreaPathPrefix { get; init; }

    /// <summary>
    /// A rectangle over the organization's own coordinates: a map viewport, or the rectangle
    /// around a circle search (14 Oct 2026). An organization with no coordinates is outside it
    /// unless it names <see cref="UnpinnedCityName"/>.
    /// </summary>
    public double? MinLatitude { get; init; }

    public double? MaxLatitude { get; init; }

    public double? MinLongitude { get; init; }

    public double? MaxLongitude { get; init; }

    /// <summary>
    /// With a rectangle: a town whose unpinned organizations are kept, compared case-insensitively.
    /// </summary>
    public string? UnpinnedCityName { get; init; }

    /// <summary>Whether all four edges of the rectangle are set.</summary>
    public bool HasBoundingBox =>
        MinLatitude is not null && MaxLatitude is not null
        && MinLongitude is not null && MaxLongitude is not null;

    /// <summary>Whether the rectangle wraps across ±180°.</summary>
    public bool BoundingBoxCrossesAntimeridian => HasBoundingBox && MinLongitude > MaxLongitude;

    /// <summary>
    /// Paths an organization may be filed <em>above</em> the narrowed subtree and still match.
    /// <para>
    /// Set only by a map viewport. An institution is filed against a place rather than pinned to
    /// a point, so a city-wide organization must survive somebody zooming into one street of it.
    /// An <c>IN</c> rather than a reversed <c>StartsWith</c>, which EF Core does not translate.
    /// </para>
    /// </summary>
    public IReadOnlyCollection<string>? AreaPathAncestors { get; init; }

    public bool? VerifiedOnly { get; init; }

    public bool? FeaturedOnly { get; init; }

    public bool? AcceptingMembers { get; init; }

    public IReadOnlyList<string>? Languages { get; init; }

    /// <summary>The caller, so their own drafts appear in their own listings and nobody else's.</summary>
    public UserId? ViewerUserId { get; init; }

    /// <summary>Include drafts and archived pages. Staff and owner listings only.</summary>
    public bool IncludeNonPublic { get; init; }

    public OrganizationSortOrder SortOrder { get; init; } = OrganizationSortOrder.MostMembers;

    public string? Cursor { get; init; }

    public int Limit { get; init; } = 20;
}
