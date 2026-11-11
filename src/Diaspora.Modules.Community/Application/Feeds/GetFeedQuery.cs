using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Policies;

namespace Diaspora.Modules.Community.Application.Feeds;

/// <summary>
/// One page of one feed.
/// <para>
/// A single query for every feed the platform has, because the feed key is data. Adding a feed
/// adds no route, no query and no handler.
/// </para>
/// </summary>
[RequirePolicy(CommunityPolicies.Read)]
public sealed record GetFeedQuery : IQuery<FeedResponse>
{
    public required string FeedKey { get; init; }

    public string? Cursor { get; init; }

    public int Limit { get; init; } = CommunityLimits.DefaultFeedPageSize;

    public string? RankingKey { get; init; }

    public string? CountryCode { get; init; }

    public Guid? AreaId { get; init; }

    public string? CityName { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }

    public double? RadiusKm { get; init; }

    public string? Category { get; init; }

    public string? Urgency { get; init; }

    public string? Hashtag { get; init; }

    public Guid? SubjectId { get; init; }

    public bool IncludeExpired { get; init; }

    /// <summary>Free-form, passed through to whichever provider owns the feed key.</summary>
    public IReadOnlyDictionary<string, string>? Parameters { get; init; }
}
