using Diaspora.Modules.Community.Domain.Comments;
using Diaspora.Modules.Community.Domain.Errors;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.Reactions;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Extensibility.Ranking;
using Diaspora.Modules.Location.Integration;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Community.Application.Services;

/// <summary>
/// Runs a feed: select, resolve signals, rank.
/// <para>
/// The order is the design. A provider selects, ranking signals are resolved in bulk, a strategy
/// orders. Keeping the three apart is what makes both halves of the brief's extensibility
/// requirement true at once — a new feed does not have to bring a ranking, and a new ranking
/// works on every existing feed.
/// </para>
/// <para>
/// <b>Distances come from Location.</b> The composer asks <c>ILocationDirectory</c> for each
/// candidate's distance from the reader and hands the answers to the ranker. B6 computes no
/// geometry beyond the rectangular prefilter the nearby provider uses to bound its query.
/// </para>
/// </summary>
public sealed class FeedComposer
{
    private readonly FeedProviderRegistry _providers;
    private readonly RankingStrategyRegistry _rankings;
    private readonly IReactionRepository _reactions;
    private readonly ICommentRepository _comments;
    private readonly ILocationDirectory _location;
    private readonly ITrustService _trust;
    private readonly IClock _clock;
    private readonly ILogger<FeedComposer> _logger;

    public FeedComposer(
        FeedProviderRegistry providers,
        RankingStrategyRegistry rankings,
        IReactionRepository reactions,
        ICommentRepository comments,
        ILocationDirectory location,
        ITrustService trust,
        IClock clock,
        ILogger<FeedComposer> logger)
    {
        _providers = providers;
        _rankings = rankings;
        _reactions = reactions;
        _comments = comments;
        _location = location;
        _trust = trust;
        _clock = clock;
        _logger = logger;
    }

    public IReadOnlyList<IFeedProvider> Providers =>
        _providers.Keys.Select(key => _providers.Find(key)!).ToList();

    public IReadOnlyList<string> RankingKeys => _rankings.Keys;

    public async Task<Result<(FeedPage Page, string RankingKey)>> ComposeAsync(
        FeedRequest request, ViewerContext viewer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(viewer);

        var provider = _providers.Find(request.FeedKey);

        if (provider is null)
        {
            return Result.Failure<(FeedPage, string)>(
                CommunityErrors.FeedUnknown(request.FeedKey ?? string.Empty));
        }

        if (!provider.CanServe(request))
        {
            return Result.Failure<(FeedPage, string)>(CommunityErrors.FeedScopeRequired);
        }

        var page = await provider.GetAsync(request, cancellationToken);

        if (page.Items.Count == 0)
        {
            return Result.Success((page, request.RankingKey ?? provider.DefaultRankingKey));
        }

        var strategy = _rankings.Resolve(request.RankingKey ?? provider.DefaultRankingKey);
        var context = await BuildContextAsync(request, viewer, page, cancellationToken);

        var ranked = strategy.Rank(page.Items, context);

        // A strategy that dropped items would silently shorten a page, which a client reads as
        // the end of the feed. Caught rather than trusted, because it is the one contract
        // violation a contributed ranking can commit that nothing else would ever notice.
        if (ranked.Count != page.Items.Count)
        {
            _logger.LogError(
                "Ranking strategy {Strategy} returned {Returned} of {Expected} items. Falling back to the provider order.",
                strategy.RankingKey,
                ranked.Count,
                page.Items.Count);

            ranked = page.Items;
        }

        var trimmed = ranked.Take(Math.Min(request.Limit, CommunityLimits.MaxFeedPageSize)).ToList();

        var next = trimmed.Count < ranked.Count || page.NextCursor is not null
            ? FeedCursor.Create(trimmed[^1].SortKey, trimmed[^1].Reference.Id)
            : null;

        return Result.Success((FeedPage.Of(trimmed, next), strategy.RankingKey));
    }

    private async Task<RankingContext> BuildContextAsync(
        FeedRequest request,
        ViewerContext viewer,
        FeedPage page,
        CancellationToken cancellationToken)
    {
        var references = page.Items.Select(item => item.Reference).ToList();

        var summaries = await _reactions.GetSummariesAsync(references, cancellationToken);
        var commentCounts = await _comments.CountManyAsync(references, cancellationToken);

        var authorIds = page.Items
            .Where(item => item.Post is not null)
            .Select(item => item.Post!.AuthorId)
            .Distinct()
            .ToList();

        var trustLevels = new Dictionary<Guid, int>();

        foreach (var authorId in authorIds)
        {
            trustLevels[authorId.Value] = (int)await _trust.GetTrustLevelAsync(
                authorId, cancellationToken);
        }

        return new RankingContext
        {
            Request = request,
            Now = _clock.UtcNow,
            AuthorTrustLevels = trustLevels,
            Distances = ResolveDistances(request, page),
            ReactionScores = summaries.ToDictionary(
                entry => entry.Key, entry => entry.Value.WeightedScore, StringComparer.Ordinal),
            CommentCounts = commentCounts,
            FollowedAuthorIds = viewer.FollowedMemberIds
        };
    }

    /// <summary>
    /// Distances from the reader to each candidate, computed by Location.
    /// <para>
    /// One call per candidate into Location's own distance function, which is a pure calculation
    /// rather than a query — the alternative would be B6 implementing the haversine itself, and
    /// the whole point of the seam is that it does not.
    /// </para>
    /// </summary>
    private Dictionary<string, double> ResolveDistances(FeedRequest request, FeedPage page)
    {
        var distances = new Dictionary<string, double>(StringComparer.Ordinal);

        if (!request.HasCoordinates)
        {
            return distances;
        }

        var origin = Modules.Location.Domain.ValueObjects.GeoCoordinate.Create(
            request.Latitude!.Value, request.Longitude!.Value);

        foreach (var item in page.Items)
        {
            if (item.Post?.Location is not { HasCoordinates: true } location)
            {
                continue;
            }

            var destination = Modules.Location.Domain.ValueObjects.GeoCoordinate.Create(
                location.Latitude!.Value, location.Longitude!.Value);

            distances[item.Reference.Key] = _location.DistanceBetween(origin, destination).Kilometers;
        }

        return distances;
    }
}
