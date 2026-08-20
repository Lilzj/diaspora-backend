using Diaspora.Modules.Community.Extensibility.Feeds;
using Diaspora.Modules.Community.Domain.Enums;

namespace Diaspora.Modules.Community.Extensibility.Ranking;

/// <summary>
/// Chronological, adjusted by what Trust says about the author and what the community says
/// about the post.
/// <para>
/// <b>B6 makes no trust decisions here.</b> The author's level arrives pre-resolved in the
/// context, having come from the kernel's <c>ITrustService</c>, which Trust owns. This strategy
/// only decides how much a level is worth <em>to an ordering</em>, which is a feed concern and
/// not a trust one. The brief forbids duplicating trust logic; consuming a level and multiplying
/// by it is consumption.
/// </para>
/// <para>
/// The community-verification multiplier is the larger of the two, deliberately. A post from an
/// unverified member that ten neighbours have confirmed is better information than an
/// unconfirmed post from a well-regarded one, and a ranking that said otherwise would be
/// ranking reputations rather than information.
/// </para>
/// <para>
/// A disputed post is <b>demoted, not hidden</b>. Hiding it would make a live dispute invisible
/// to exactly the people arguing about it; demotion keeps it findable while stopping it
/// spreading.
/// </para>
/// </summary>
public sealed class TrustWeightedRanking : IRankingStrategy
{
    private const double VerifiedContentMultiplier = 1.6d;

    private const double DisputedContentMultiplier = 0.4d;

    private const double FollowedAuthorMultiplier = 1.25d;

    public string RankingKey => RankingKeys.TrustWeighted;

    public IReadOnlyList<FeedItem> Rank(IReadOnlyList<FeedItem> items, RankingContext context)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(context);

        return items
            .Select(item => item with { SortKey = Score(item, context) })
            .OrderByDescending(item => item.SortKey)
            .ToList();
    }

    private static double Score(FeedItem item, RankingContext context)
    {
        var recency = (item.PublishedAt ?? context.Now).ToUnixTimeMilliseconds() / 1_000d;
        var multiplier = 1d;

        if (item.Post is { } post)
        {
            multiplier *= post.VerificationStatus switch
            {
                PostVerificationStatus.CommunityVerified => VerifiedContentMultiplier,
                PostVerificationStatus.Disputed => DisputedContentMultiplier,
                _ => 1d
            };

            var authorId = post.AuthorId.Value;

            // Trust's four-rung platform ladder, as an ordinal. None leaves the multiplier at
            // one; each rung above adds a tenth. Small on purpose: trust should tilt a feed,
            // not determine it, or a new member's genuinely urgent post never surfaces.
            multiplier *= 1d + (context.AuthorTrustLevels.GetValueOrDefault(authorId) * 0.1d);

            if (context.FollowedAuthorIds.Contains(authorId))
            {
                multiplier *= FollowedAuthorMultiplier;
            }
        }

        return recency * multiplier;
    }
}
