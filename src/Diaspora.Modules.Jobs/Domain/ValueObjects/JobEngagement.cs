using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// The counters a posting card renders, denormalised onto the aggregate.
/// <para>
/// <b>Denormalised deliberately, and the reason is the read path.</b> A results page of twenty
/// postings showing an application count is twenty aggregate reads or one column; a job with
/// four thousand applicants must not be loaded to render "4,000 applicants". The authoritative
/// figures live in <c>JobAnalytics</c>, which is a separate aggregate and can be rebuilt; these
/// are the fast copy.
/// </para>
/// <para>
/// <b>Nothing here bumps the posting's revision.</b> A candidate viewing a job is not an edit,
/// and must never make an employer's in-flight save fail with a concurrency conflict — the same
/// rule B8 states for guide engagement, and the one that makes the whole counter design safe.
/// </para>
/// <para>
/// Every mutator returns a new instance and floors at zero: a decrement arriving twice through
/// at-least-once delivery must not produce a negative applicant count on a card.
/// </para>
/// </summary>
public sealed class JobEngagement : ValueObject
{
    private JobEngagement(
        long viewCount,
        long uniqueViewCount,
        int applicationCount,
        int saveCount,
        long shareCount,
        long searchImpressions,
        long searchClicks)
    {
        ViewCount = viewCount;
        UniqueViewCount = uniqueViewCount;
        ApplicationCount = applicationCount;
        SaveCount = saveCount;
        ShareCount = shareCount;
        SearchImpressions = searchImpressions;
        SearchClicks = searchClicks;
    }

    private JobEngagement() { }

    public long ViewCount { get; private set; }

    /// <summary>Distinct viewers, as far as the recorder can tell.</summary>
    public long UniqueViewCount { get; private set; }

    public int ApplicationCount { get; private set; }

    public int SaveCount { get; private set; }

    public long ShareCount { get; private set; }

    /// <summary>Times this posting appeared in a result set.</summary>
    public long SearchImpressions { get; private set; }

    /// <summary>Times someone opened it from a result set.</summary>
    public long SearchClicks { get; private set; }

    /// <summary>What every posting starts with.</summary>
    // Fresh per call: a shared owned instance is tracked against one owner only. See SalaryRange.
    public static JobEngagement None => new(0, 0, 0, 0, 0, 0, 0);

    public JobEngagement WithView(bool unique = false) =>
        new(ViewCount + 1, UniqueViewCount + (unique ? 1 : 0), ApplicationCount, SaveCount,
            ShareCount, SearchImpressions, SearchClicks);

    public JobEngagement WithApplicationDelta(int delta) =>
        new(ViewCount, UniqueViewCount, Math.Max(0, ApplicationCount + delta), SaveCount,
            ShareCount, SearchImpressions, SearchClicks);

    public JobEngagement WithSaveDelta(int delta) =>
        new(ViewCount, UniqueViewCount, ApplicationCount, Math.Max(0, SaveCount + delta),
            ShareCount, SearchImpressions, SearchClicks);

    public JobEngagement WithShare() =>
        new(ViewCount, UniqueViewCount, ApplicationCount, SaveCount, ShareCount + 1,
            SearchImpressions, SearchClicks);

    public JobEngagement WithImpressions(long count) =>
        new(ViewCount, UniqueViewCount, ApplicationCount, SaveCount, ShareCount,
            SearchImpressions + Math.Max(0, count), SearchClicks);

    public JobEngagement WithSearchClick() =>
        new(ViewCount, UniqueViewCount, ApplicationCount, SaveCount, ShareCount,
            SearchImpressions, SearchClicks + 1);

    /// <summary>
    /// Applications as a share of views. The funnel number an employer dashboard leads with,
    /// and the one that tells an employer their posting is being read and rejected rather than
    /// never found. Zero when nobody has looked.
    /// </summary>
    public double ApplicationRate =>
        ViewCount == 0 ? 0d : (double)ApplicationCount / ViewCount;

    /// <summary>Opens as a share of appearances in results. Measures the card, not the job.</summary>
    public double ClickThroughRate =>
        SearchImpressions == 0 ? 0d : (double)SearchClicks / SearchImpressions;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ViewCount;
        yield return UniqueViewCount;
        yield return ApplicationCount;
        yield return SaveCount;
        yield return ShareCount;
        yield return SearchImpressions;
        yield return SearchClicks;
    }
}
