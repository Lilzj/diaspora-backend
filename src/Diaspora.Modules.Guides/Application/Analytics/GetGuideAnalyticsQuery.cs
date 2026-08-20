using Diaspora.Application.Abstractions;
using Diaspora.Modules.Guides.Contracts.V1.Responses;

namespace Diaspora.Modules.Guides.Application.Analytics;

/// <summary>
/// One guide's accumulated usage, for the person who wrote it.
/// <para>
/// The author sees their own; staff see any. Not public, and not because the numbers are
/// sensitive — because they are <em>theirs</em>: readership is what tells an author whether the
/// hours they spent on a residence-permit guide reached anybody, and it is not a leaderboard for
/// everybody else to browse.
/// </para>
/// </summary>
[Authorize(Permission = GuidePermissions.ViewAnalytics)]
public sealed record GetGuideAnalyticsQuery : IQuery<GuideAnalyticsResponse>
{
    public required Guid GuideId { get; init; }
}
