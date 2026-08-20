using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Community.Extensibility.Feeds;

/// <summary>
/// Everything a feed provider is given.
/// <para>
/// Note what is <b>not</b> here: a <c>DbContext</c>, an <c>IQueryable</c>, or a repository. A
/// provider composes a <see cref="Domain.Posts.PostQueryFilter"/> and hands it back, or
/// assembles items some other way entirely. That constraint is what lets a provider ship in
/// another assembly without inheriting this module's persistence.
/// </para>
/// <para>
/// <see cref="AllowedVisibility"/> is computed once, before any provider runs, and handed to all
/// of them. Letting each provider work out the viewer's audience independently would mean a bug
/// in one of them leaks content in one feed and not another — which is the hardest possible
/// version of that bug to notice.
/// </para>
/// </summary>
public sealed record FeedRequest
{
    public required string FeedKey { get; init; }

    /// <summary>Null for a signed-out reader. Only public content is eligible.</summary>
    public UserId? ViewerId { get; init; }

    /// <summary>The audiences this viewer qualifies for. Computed centrally, never by a provider.</summary>
    public required IReadOnlyCollection<VisibilityLevel> AllowedVisibility { get; init; }

    /// <summary>Authors the viewer has blocked, or who have blocked the viewer.</summary>
    public IReadOnlyCollection<Guid> ExcludedAuthorIds { get; init; } = [];

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public string? HierarchyPathPrefix { get; init; }

    public string? CityName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double? RadiusKm { get; init; }

    public PostCategory? Category { get; init; }

    public UrgencyLevel? MinimumUrgency { get; init; }

    public string? HashtagSlug { get; init; }

    /// <summary>Set for a contributor or organization feed.</summary>
    public Guid? SubjectId { get; init; }

    /// <summary>Which ranking to apply. Null means the provider's own default.</summary>
    public string? RankingKey { get; init; }

    public bool IncludeExpired { get; init; }

    public FeedCursor? Cursor { get; init; }

    public int Limit { get; init; } = CommunityLimits.DefaultFeedPageSize;

    /// <summary>Free-form parameters a contributed provider defines for itself.</summary>
    public IReadOnlyDictionary<string, string> Parameters { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public bool HasCoordinates => Latitude is not null && Longitude is not null;
}
