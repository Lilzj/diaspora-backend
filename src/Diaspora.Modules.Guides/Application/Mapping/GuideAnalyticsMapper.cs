using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Analytics;

namespace Diaspora.Modules.Guides.Application.Mapping;

/// <summary>
/// Turns an analytics row into the wire shape an author's dashboard renders.
/// <para>
/// Measurement, not state: every number here is an accumulation that may lose a count under
/// contention, and unique views are an approximation by design. The mapper does no arithmetic on
/// them — no averages, no rates — because a derived figure computed in two places drifts, and
/// the client can divide.
/// </para>
/// <para>
/// A guide with no row has never been read, which is a zero rather than a missing answer:
/// <see cref="Empty"/> exists so a fresh guide's dashboard renders like any other instead of
/// making the caller special-case it.
/// </para>
/// </summary>
public static class GuideAnalyticsMapper
{
    public static GuideAnalyticsResponse ToResponse(GuideAnalytics analytics)
    {
        ArgumentNullException.ThrowIfNull(analytics);

        return new GuideAnalyticsResponse
        {
            GuideId = analytics.GuideId.Value,
            Views = analytics.Views,
            UniqueViews = analytics.UniqueViews,
            Completions = analytics.Completions,
            TotalReadSeconds = analytics.TotalReadSeconds,
            Bookmarks = analytics.Bookmarks,
            Shares = analytics.Shares,
            HelpfulVotes = analytics.HelpfulVotes,
            NotHelpfulVotes = analytics.NotHelpfulVotes,
            SearchImpressions = analytics.SearchImpressions,
            SearchClicks = analytics.SearchClicks,
            VersionAdoptions = analytics.VersionAdoptions,
            LastViewedAt = analytics.LastViewedAt
        };
    }

    /// <summary>
    /// All zeroes for a guide whose analytics row does not exist yet. The row is created on the
    /// first recorded view, and a read must never create one — a dashboard visit is not a read
    /// of the guide.
    /// </summary>
    public static GuideAnalyticsResponse Empty(Guid guideId) => new()
    {
        GuideId = guideId,
        Views = 0,
        UniqueViews = 0,
        Completions = 0,
        TotalReadSeconds = 0,
        Bookmarks = 0,
        Shares = 0,
        HelpfulVotes = 0,
        NotHelpfulVotes = 0,
        SearchImpressions = 0,
        SearchClicks = 0,
        VersionAdoptions = 0,
        LastViewedAt = null
    };
}
