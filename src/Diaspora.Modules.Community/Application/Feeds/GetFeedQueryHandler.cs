using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Policies;
using Diaspora.Modules.Community.Domain.ValueObjects;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Integration;
using Diaspora.Modules.Location.Integration;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Community.Application.Feeds;

/// <summary>
/// Serves a feed.
/// <para>
/// The reader's context and their allowed audiences are resolved here, once, and pushed into the
/// request â€” so no feed provider decides for itself who may see what, and a contributed provider
/// cannot leak content by forgetting to filter.
/// </para>
/// <para>
/// Loose geographic parameters go through Location's scope resolver before the provider sees
/// them, which is what turns "Peckham" into a country, an area and a bounded search without B6
/// knowing anything about geography.
/// </para>
/// </summary>
public sealed class GetFeedQueryHandler : IQueryHandler<GetFeedQuery, FeedResponse>
{
    /// <summary>The nearby feed's radius when a request names none. See <c>NearbyFeedProvider</c>.</summary>
    private const double NearbyFeedDefaultRadiusKm = 25d;

    private readonly FeedComposer _composer;
    private readonly ViewerContextResolver _viewers;
    private readonly IGeographicScopeResolver _scopes;
    private readonly ICommunitySocialService _social;
    private readonly CommunityAuthorNames _authorNames;

    public GetFeedQueryHandler(
        FeedComposer composer,
        ViewerContextResolver viewers,
        IGeographicScopeResolver scopes,
        ICommunitySocialService social,
        CommunityAuthorNames authorNames)
    {
        _composer = composer;
        _viewers = viewers;
        _scopes = scopes;
        _social = social;
        _authorNames = authorNames;
    }

    public async Task<Result<FeedResponse>> Handle(
        GetFeedQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        // ── Without the catalogue — 14 Oct 2026 ─────────────────────────────────────────────
        //
        // This resolved through the catalogue and handed the provider the node the point snapped
        // to, and the nearby feed narrowed to exactly that node's area id: "posts near me" meant
        // "posts filed against the nearest catalogued neighbourhood", and a post pinned in the next
        // street, filed against another node or none, was not near anybody. A point is now the
        // rectangle around its circle, a town is its name, and an area id from an old app build is
        // accepted and ignored. The nearby feed's own default radius stands when none was sent.
        var scope = await _scopes.ResolveForDiscoveryAsync(
            new GeographicScopeRequest
            {
                CityName = request.CityName,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RadiusKm = request.RadiusKm ?? NearbyFeedDefaultRadiusKm,
                CountryCode = request.CountryCode,
                UserId = viewer.UserId
            },
            cancellationToken);

        var resolved = scope.IsSuccess ? scope.Value : GeographicScope.Unbounded;

        var feedRequest = new FeedRequest
        {
            FeedKey = request.FeedKey,
            ViewerId = viewer.UserId,
            AllowedVisibility = ViewerContextResolver.AllowedLevels(viewer),
            NeighborhoodViewer = viewer.IsAuthenticated
                ? new NeighborhoodAudienceViewer
                {
                    Latitude = viewer.Latitude,
                    Longitude = viewer.Longitude,
                    Countries = viewer.CountryCode is null
                        ? [.. viewer.VerifiedCountries]
                        : [viewer.CountryCode, .. viewer.VerifiedCountries]
                }
                : null,
            ExcludedAuthorIds = viewer.BlockedMemberIds,
            CountryCode = resolved.CountryCode?.Value ?? request.CountryCode,
            AreaId = null,
            HierarchyPathPrefix = null,
            CityName = resolved.CityName ?? request.CityName,
            Latitude = resolved.Center?.Latitude ?? request.Latitude,
            Longitude = resolved.Center?.Longitude ?? request.Longitude,
            RadiusKm = request.RadiusKm,
            BoundingBox = resolved.IsAroundPoint && resolved.Bounds is { } box
                ? (box.South, box.West, box.North, box.East)
                : null,
            Category = string.IsNullOrWhiteSpace(request.Category)
                ? null
                : CommunityWireNames.ParseOr(request.Category, PostCategory.Other),
            MinimumUrgency = string.IsNullOrWhiteSpace(request.Urgency)
                ? null
                : CommunityWireNames.ParseOr(request.Urgency, UrgencyLevel.Low),
            HashtagSlug = request.Hashtag,
            SubjectId = request.SubjectId,
            RankingKey = request.RankingKey,
            IncludeExpired = request.IncludeExpired,
            Cursor = FeedCursor.Decode(request.Cursor),
            Limit = Math.Clamp(request.Limit, 1, CommunityLimits.MaxFeedPageSize),
            Parameters = request.Parameters
                ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };

        var composed = await _composer.ComposeAsync(feedRequest, viewer, cancellationToken);

        if (composed.IsFailure)
        {
            return Result.Failure<FeedResponse>(composed.Error);
        }

        var (page, rankingKey) = composed.Value;

        var social = await _social.GetSummariesAsync(
            page.Items.Select(item => item.Reference).ToList(), viewer.UserId, cancellationToken);

        // Every author on the page in one lookup.
        var names = await _authorNames.ResolveAsync(
            page.Items.Where(item => item.Post is not null).Select(item => item.Post!.AuthorId),
            cancellationToken);

        return Result.Success(
            FeedMapper.ToResponse(request.FeedKey, rankingKey, page, viewer, social, names));
    }
}
