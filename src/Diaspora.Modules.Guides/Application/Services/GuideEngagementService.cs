using Diaspora.Modules.Guides.Domain.Analytics;
using Diaspora.Modules.Guides.Domain.Guides;

namespace Diaspora.Modules.Guides.Application.Services;

/// <summary>
/// Applies engagement changes to the guide's rollup and the analytics row together.
/// <para>
/// The two records answer different questions from the same events — the rollup renders on
/// cards, the analytics row reports to the author — and a handler that updated one and forgot
/// the other would make them drift apart in a way nobody notices until the numbers disagree
/// on one screen. One method per event is what keeps them in step.
/// </para>
/// <para>
/// Static: there is no state, only arithmetic, and the counter mutators deliberately bump no
/// revision and raise no update event — a reader engaging with a guide is not an edit.
/// </para>
/// </summary>
public static class GuideEngagementService
{
    /// <summary>A vote was cast, changed or withdrawn. Deltas may be negative.</summary>
    public static void ApplyVote(
        Guide guide, GuideAnalytics? analytics, int helpfulDelta, int notHelpfulDelta)
    {
        ArgumentNullException.ThrowIfNull(guide);

        guide.ApplyVoteDelta(helpfulDelta, notHelpfulDelta);
        analytics?.ApplyVoteDelta(helpfulDelta, notHelpfulDelta);
    }

    /// <summary>
    /// Community's count of saves, replaced rather than incremented. Delivery is at-least-once,
    /// so a +1 per message would climb each time one was redelivered; a recount cannot.
    /// </summary>
    public static void ApplyBookmarkCount(Guide guide, GuideAnalytics? analytics, int count)
    {
        ArgumentNullException.ThrowIfNull(guide);

        guide.ApplyBookmarkCount(count);
        analytics?.ApplyBookmarkCount(count);
    }

    /// <summary>
    /// Community's comment count, replaced rather than incremented — Community owns it, and a
    /// recount cannot drift the way a delta stream can.
    /// </summary>
    public static void ApplyCommentCount(Guide guide, int count)
    {
        ArgumentNullException.ThrowIfNull(guide);

        guide.ApplyCommentCount(count);
    }

    /// <summary>A reader shared the guide.</summary>
    public static void RecordShare(Guide guide, GuideAnalytics? analytics)
    {
        ArgumentNullException.ThrowIfNull(guide);

        guide.RecordShare();
        analytics?.RecordShare();
    }
}
