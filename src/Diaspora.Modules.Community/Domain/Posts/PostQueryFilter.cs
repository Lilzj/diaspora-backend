using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Domain.Posts;

/// <summary>
/// Everything a feed can ask the post table for.
/// <para>
/// <b>This type is why feed providers are safe to contribute.</b> A provider composes a filter
/// and hands it back; it never composes an expression tree, never touches a
/// <c>DbContext</c>, and cannot write a predicate EF fails to translate at run time inside
/// another module's stack trace. Every field here is one the repository knows how to translate,
/// which is a promise the repository can actually keep.
/// </para>
/// <para>
/// The bounding box is the one piece of geometry B6 computes for itself, and it is a
/// <em>prefilter</em>: it narrows candidate rows to a rectangle so that Location is asked for
/// real distances about tens of posts rather than tens of thousands. The answer to "how far" is
/// still Location's, and B6 never rounds, sorts or renders a distance it worked out itself.
/// </para>
/// </summary>
public sealed record PostQueryFilter
{
    /// <summary>Which statuses count. Published only, unless a caller says otherwise.</summary>
    public IReadOnlyCollection<PostStatus> Statuses { get; init; } = [PostStatus.Published];

    public IReadOnlyCollection<UserId>? AuthorIds { get; init; }

    public IReadOnlyCollection<Guid>? OrganizationIds { get; init; }

    public IReadOnlyCollection<CommunityPostId>? PostIds { get; init; }

    public PostCategory? Category { get; init; }

    public UrgencyLevel? MinimumUrgency { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    /// <summary>Prefix-matched against the copied hierarchy path. A subtree of geography.</summary>
    public string? HierarchyPathPrefix { get; init; }

    public string? CityName { get; init; }

    public string? HashtagSlug { get; init; }

    /// <summary>The audiences a viewer is eligible for. Computed before the query, never in it.</summary>
    public IReadOnlyCollection<VisibilityLevel>? AllowedVisibility { get; init; }

    /// <summary>Excludes posts by these authors. How blocking reaches the feed.</summary>
    public IReadOnlyCollection<Guid>? ExcludedAuthorIds { get; init; }

    public bool ExcludeExpired { get; init; } = true;

    public bool PinnedFirst { get; init; }

    public DateTimeOffset? PublishedAfter { get; init; }

    public DateTimeOffset? PublishedBefore { get; init; }

    /// <summary>South-west and north-east corners of the prefilter rectangle.</summary>
    public (double MinLatitude, double MinLongitude, double MaxLatitude, double MaxLongitude)?
        BoundingBox { get; init; }

    /// <summary>
    /// With <see cref="BoundingBox"/>: a town whose posts with no coordinates are kept, compared
    /// case-insensitively. A circle search that also named a town still finds them (14 Oct 2026).
    /// </summary>
    public string? UnpinnedCityName { get; init; }

    /// <summary>True narrows to posts that declared an affected area. The nearby query's second look.</summary>
    public bool? HasAffectedArea { get; init; }

    public FeedCursor? Cursor { get; init; }

    public int Limit { get; init; } = CommunityLimits.DefaultFeedPageSize;

    /// <summary>
    /// Whether the repository orders by publication time or leaves ordering to a ranker.
    /// <para>
    /// A ranked feed still needs the database to bound the candidate set somehow, so it orders
    /// chronologically and over-fetches, then ranks. Ranking in SQL would mean every ranking
    /// strategy had to be expressible as a translatable expression, which is exactly the
    /// constraint that makes ranking algorithms unreplaceable.
    /// </para>
    /// </summary>
    public bool Chronological { get; init; } = true;

    public PostQueryFilter WithCursor(FeedCursor? cursor) => this with { Cursor = cursor };

    public PostQueryFilter WithLimit(int limit) => this with
    {
        Limit = Math.Clamp(limit, 1, CommunityLimits.MaxFeedPageSize)
    };
}
