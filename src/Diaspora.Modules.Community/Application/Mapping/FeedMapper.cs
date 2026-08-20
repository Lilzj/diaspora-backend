using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Contracts.V1.Responses;
using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Integration;

namespace Diaspora.Modules.Community.Application.Mapping;

/// <summary>Projects a feed page.</summary>
public static class FeedMapper
{
    public static FeedResponse ToResponse(
        string feedKey,
        string rankingKey,
        FeedPage page,
        ViewerContext viewer,
        IReadOnlyDictionary<string, SocialSummary> social)
    {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentNullException.ThrowIfNull(social);

        return new FeedResponse
        {
            FeedKey = feedKey,
            RankingKey = rankingKey,
            Items = page.Items
                .Select(item => new FeedItemResponse
                {
                    ContentType = CommunityWireNames.ToWire(item.Reference.Type),
                    ContentId = item.Reference.Id,
                    Post = item.Post is null
                        ? null
                        : PostMapper.ToSummary(
                            item.Post,
                            social.GetValueOrDefault(item.Reference.Key),
                            viewer,
                            item.DistanceKm),
                    DistanceKm = item.DistanceKm,
                    Reason = item.Reason
                })
                .ToList(),
            NextCursor = page.NextCursor?.Encode(),
            HasMore = page.HasMore,
            TotalAvailable = page.TotalAvailable
        };
    }
}
