using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Guides.Domain.Analytics;

/// <summary>
/// The accumulating usage counters for one guide. Unique per guide.
/// <para>
/// A separate aggregate rather than columns on the guide, because these counters are written
/// on the read path — every view is a write here — and a row that hot must not share a lock
/// with the row an author is editing. Nothing here is a source of truth for anything except
/// itself: it is measurement, not state, and losing a count under contention is acceptable in
/// a way that losing an edit is not.
/// </para>
/// <para>
/// Unique views are an approximation by design — plus one when the client says this is a
/// first view. Honest analytics at the cost the platform is willing to pay, not fingerprinting.
/// </para>
/// </summary>
public sealed class GuideAnalytics : AggregateRoot<GuideAnalyticsId>
{
    private GuideAnalytics(GuideAnalyticsId id, GuideId guideId)
        : base(id)
    {
        GuideId = guideId;
    }

    private GuideAnalytics() { }

    public GuideId GuideId { get; private set; } = default!;

    public long Views { get; private set; }

    public long UniqueViews { get; private set; }

    /// <summary>Times a reader reached the end, when the client reports it.</summary>
    public long Completions { get; private set; }

    public long TotalReadSeconds { get; private set; }

    public int Bookmarks { get; private set; }

    public int Shares { get; private set; }

    public int HelpfulVotes { get; private set; }

    public int NotHelpfulVotes { get; private set; }

    public long SearchImpressions { get; private set; }

    public long SearchClicks { get; private set; }

    /// <summary>Times an old version was restored. A signal that recent edits made things worse.</summary>
    public int VersionAdoptions { get; private set; }

    public DateTimeOffset? LastViewedAt { get; private set; }

    public static Result<GuideAnalytics> Create(GuideAnalyticsId id, GuideId guideId)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(guideId);

        return Result.Success(new GuideAnalytics(id, guideId));
    }

    public void RecordView(bool unique, DateTimeOffset now)
    {
        Views++;

        if (unique)
        {
            UniqueViews++;
        }

        LastViewedAt = now;
    }

    public void RecordCompletion(long readSeconds)
    {
        Completions++;
        TotalReadSeconds += Math.Max(0, readSeconds);
    }

    public void RecordReadTime(long readSeconds) =>
        TotalReadSeconds += Math.Max(0, readSeconds);

    public void ApplyBookmarkDelta(int delta) => Bookmarks = Math.Max(0, Bookmarks + delta);

    public void RecordShare() => Shares++;

    public void ApplyVoteDelta(int helpfulDelta, int notHelpfulDelta)
    {
        HelpfulVotes = Math.Max(0, HelpfulVotes + helpfulDelta);
        NotHelpfulVotes = Math.Max(0, NotHelpfulVotes + notHelpfulDelta);
    }

    public void RecordSearchImpressions(int count) =>
        SearchImpressions += Math.Max(0, count);

    public void RecordSearchClick() => SearchClicks++;

    public void RecordVersionAdoption() => VersionAdoptions++;
}
