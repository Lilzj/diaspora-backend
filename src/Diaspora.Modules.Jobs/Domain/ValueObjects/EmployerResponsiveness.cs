using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.ValueObjects;

/// <summary>
/// How an employer actually behaves towards applicants, measured rather than claimed.
/// <para>
/// <b>This is the number that changes candidate behaviour, and it is the one job boards refuse
/// to publish.</b> Applying into silence is the defining experience of job hunting, and it is
/// worst for exactly the people this platform serves — applying across a border, unable to
/// follow up in person, with no way to tell a real vacancy from a stale advert. An employer's
/// median time to first response and its ghosting rate are facts the platform already holds;
/// surfacing them is the strongest single incentive it can create for employers to behave well.
/// </para>
/// <para>
/// <b>A rolling aggregate, not a history.</b> Storing every response interval would make the
/// employer aggregate grow without bound; storing a count and a running total gives the mean at
/// constant size. The trade is that it cannot be recomputed from itself if it drifts — which is
/// why <c>JobAnalytics</c> keeps the per-posting figures this is derived from, and a rebuild is
/// always possible from there.
/// </para>
/// </summary>
public sealed class EmployerResponsiveness : ValueObject
{
    private EmployerResponsiveness(
        int respondedCount,
        long totalResponseMinutes,
        int decidedCount,
        int ignoredCount,
        int offersExtended,
        int offersAccepted)
    {
        RespondedCount = respondedCount;
        TotalResponseMinutes = totalResponseMinutes;
        DecidedCount = decidedCount;
        IgnoredCount = ignoredCount;
        OffersExtended = offersExtended;
        OffersAccepted = offersAccepted;
    }

    private EmployerResponsiveness() { }

    /// <summary>Applications this employer answered at all.</summary>
    public int RespondedCount { get; private set; }

    /// <summary>The running total of first-response times, in minutes.</summary>
    public long TotalResponseMinutes { get; private set; }

    /// <summary>Applications this employer took to a decision, either way.</summary>
    public int DecidedCount { get; private set; }

    /// <summary>Applications left unanswered past the platform's staleness threshold.</summary>
    public int IgnoredCount { get; private set; }

    public int OffersExtended { get; private set; }

    public int OffersAccepted { get; private set; }

    /// <summary>What a new employer starts with. No record either way.</summary>
    // Fresh per call: a shared owned instance is tracked against one owner only. See SalaryRange.
    public static EmployerResponsiveness None => new(0, 0, 0, 0, 0, 0);

    /// <summary>
    /// Records how long this employer took to respond to an application for the first time.
    /// Negative intervals — a clock skew, a backfill — are dropped rather than trusted.
    /// </summary>
    public EmployerResponsiveness WithResponse(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            return this;
        }

        return new EmployerResponsiveness(
            RespondedCount + 1,
            TotalResponseMinutes + (long)elapsed.TotalMinutes,
            DecidedCount,
            IgnoredCount,
            OffersExtended,
            OffersAccepted);
    }

    /// <summary>An application reached a terminal state at the employer's hand.</summary>
    public EmployerResponsiveness WithDecision() =>
        new(RespondedCount, TotalResponseMinutes, DecidedCount + 1, IgnoredCount,
            OffersExtended, OffersAccepted);

    /// <summary>An application went unanswered long enough to count against the employer.</summary>
    public EmployerResponsiveness WithIgnored() =>
        new(RespondedCount, TotalResponseMinutes, DecidedCount, IgnoredCount + 1,
            OffersExtended, OffersAccepted);

    public EmployerResponsiveness WithOfferExtended() =>
        new(RespondedCount, TotalResponseMinutes, DecidedCount, IgnoredCount,
            OffersExtended + 1, OffersAccepted);

    public EmployerResponsiveness WithOfferAccepted() =>
        new(RespondedCount, TotalResponseMinutes, DecidedCount, IgnoredCount,
            OffersExtended, OffersAccepted + 1);

    /// <summary>
    /// Mean hours to a first response. Null when the employer has answered nothing — an
    /// unknown figure, which is not the same as a fast one and must not render as zero.
    /// </summary>
    public double? AverageResponseHours =>
        RespondedCount == 0 ? null : TotalResponseMinutes / (double)RespondedCount / 60d;

    /// <summary>
    /// The share of applications this employer never answered. Null until there is enough to
    /// say anything: a single ignored application is not a pattern, and publishing it as
    /// "100% ghosted" would be a libel the platform generated itself.
    /// </summary>
    public double? GhostingRate
    {
        get
        {
            var total = RespondedCount + IgnoredCount;

            return total < MinimumSampleForRates ? null : IgnoredCount / (double)total;
        }
    }

    /// <summary>The share of offers candidates accepted. Null below the sample threshold.</summary>
    public double? OfferAcceptanceRate =>
        OffersExtended < MinimumSampleForRates ? null : OffersAccepted / (double)OffersExtended;

    /// <summary>
    /// The smallest sample the platform will publish a rate from. Five is arbitrary but the
    /// principle is not: a rate computed from one or two events is noise presented as a fact,
    /// and it is an employer's reputation being described.
    /// </summary>
    private const int MinimumSampleForRates = 5;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RespondedCount;
        yield return TotalResponseMinutes;
        yield return DecidedCount;
        yield return IgnoredCount;
        yield return OffersExtended;
        yield return OffersAccepted;
    }
}
