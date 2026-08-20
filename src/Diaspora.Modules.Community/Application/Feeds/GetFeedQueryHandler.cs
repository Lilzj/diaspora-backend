using Diaspora.Application.Abstractions;
using Diaspora.Modules.Community.Application.Mapping;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
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
    private readonly FeedComposer _composer;
    private readonly ViewerContextResolver _viewers;
    private readonly IGeographicScopeResolver _scopes;
    private readonly ICommunitySocialService _social;

    public GetFeedQueryHandler(
        FeedComposer composer,
        ViewerContextResolver viewers,
        IGeographicScopeResolver scopes,
        ICommunitySocialService social)
    {
        _composer = composer;
        _viewers = viewers;
        _scopes = scopes;
        _social = social;
    }

    public async Task<Result<FeedResponse>> Handle(
        GetFeedQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var viewer = await _viewers.ResolveAsync(cancellationToken);

        var scope = await _scopes.ResolveAsync(
            new GeographicScopeRequest
            {
                CityName = request.CityName,
                Latitude = request.Latitude,
                Longitude = request.Longitude,
                RadiusKm = request.RadiusKm,
                CountryCode = request.CountryCode,
                AreaId = request.AreaId is { } areaId
                    ? SharedKernel.Identity.AreaId.From(areaId)
                    : null,
                UserId = viewer.UserId
            },
            cancellationToken);

        var resolved = scope.IsSuccess ? scope.Value : GeographicScope.Unbounded;

        var feedRequest = new FeedRequest
        {
            FeedKey = request.FeedKey,
            ViewerId = viewer.UserId,
            AllowedVisibility = ViewerContextResolver.AllowedLevels(viewer),
            ExcludedAuthorIds = viewer.BlockedMemberIds,
            CountryCode = resolved.CountryCode?.Value ?? request.CountryCode,
            AreaId = resolved.AreaId?.Value ?? request.AreaId,
            HierarchyPathPrefix = resolved.Path?.Value,
            CityName = resolved.CityName ?? request.CityName,
            Latitude = resolved.Center?.Latitude ?? request.Latitude,
            Longitude = resolved.Center?.Longitude ?? request.Longitude,
            RadiusKm = request.RadiusKm,
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

        return Result.Success(
            FeedMapper.ToResponse(request.FeedKey, rankingKey, page, viewer, social));
    }
}
