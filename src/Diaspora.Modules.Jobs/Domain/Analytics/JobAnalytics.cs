using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Jobs.Domain.Analytics;

/// <summary>
/// The measured record of one posting's performance.
/// <para>
/// <b>A separate aggregate from the posting's own counters, and the split is deliberate.</b>
/// <c>JobEngagement</c> on the posting is the fast copy a card renders; this is the
/// authoritative one, written on a different cadence, safe to rebuild, and safe to grow. Keeping
/// them apart means a view recorded a thousand times a minute never contends with a recruiter
/// editing the advert — which is the contention that makes analytics-on-the-aggregate a bad idea
/// at exactly the moment a posting becomes popular.
/// </para>
/// <para>
/// <b>Every figure here is a counter or a running total.</b> Nothing stores a per-event row, so
/// the aggregate is a fixed size no matter how successful the posting is. The trade is that the
/// funnel cannot be re-sliced by date after the fact — which is a reporting feature, not a
/// hiring one, and belongs in an analytics store rather than in the write model.
/// </para>
/// <para>
/// The whole conversion funnel the brief asks for lives here: impressions to clicks to views to
/// applications to interviews to offers to hires, plus the two time-based figures — time to
/// first response and time to hire — that tell an employer what candidates actually experience.
/// </para>
/// </summary>
public sealed class JobAnalytics : AggregateRoot<JobAnalyticsId>, IAuditableEntity
{
    private JobAnalytics(
        JobAnalyticsId id,
        JobPostingId postingId,
        EmployerProfileId employerId,
        DateTimeOffset createdAt)
        : base(id)
    {
        PostingId = postingId;
        EmployerId = employerId;
        CreatedAt = createdAt;
    }

    private JobAnalytics() { }

    public JobPostingId PostingId { get; private set; } = default!;

    /// <summary>Denormalised, so an employer-wide rollup needs no join.</summary>
    public EmployerProfileId EmployerId { get; private set; } = default!;

    // ── The funnel ──────────────────────────────────────────────────────────────────────────

    /// <summary>Times the posting appeared in a result set.</summary>
    public long SearchImpressions { get; private set; }

    /// <summary>Times someone opened it from a result set.</summary>
    public long SearchClicks { get; private set; }

    /// <summary>Detail-page views, from anywhere.</summary>
    public long ViewCount { get; private set; }

    public long UniqueViewCount { get; private set; }

    /// <summary>Times someone opened the application form. The intent signal.</summary>
    public long ApplicationStarts { get; private set; }

    public int ApplicationCount { get; private set; }

    public int WithdrawnCount { get; private set; }

    public int ShortlistedCount { get; private set; }

    public int InterviewedCount { get; private set; }

    public int OfferedCount { get; private set; }

    public int HiredCount { get; private set; }

    public int RejectedCount { get; private set; }

    public int SaveCount { get; private set; }

    public long ShareCount { get; private set; }

    // ── Timing ──────────────────────────────────────────────────────────────────────────────

    /// <summary>Running total of first-response times, in minutes. Divided by the count below.</summary>
    public long TotalResponseMinutes { get; private set; }

    /// <summary>Applications this posting's owner actually answered.</summary>
    public int RespondedCount { get; private set; }

    /// <summary>Running total of submission-to-hire times, in hours.</summary>
    public long TotalTimeToHireHours { get; private set; }

    // ── Distribution ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Applicants by country, as a compact delimited tally.
    /// <para>
    /// <b>Deliberately not a child table.</b> The geographic distribution of applicants is read
    /// as a whole or not at all, on one screen, and a row per country per posting would be a
    /// join and an index to answer a question that fits in a few hundred bytes. Written and read
    /// through <see cref="CountryTally"/>, never parsed by a caller.
    /// </para>
    /// </summary>
    public string? ApplicantCountryTally { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ── Derived rates ───────────────────────────────────────────────────────────────────────

    /// <summary>Opens as a share of appearances in results. Measures the card, not the job.</summary>
    public double ClickThroughRate =>
        SearchImpressions == 0 ? 0d : (double)SearchClicks / SearchImpressions;

    /// <summary>Applications as a share of views. The number an employer dashboard leads with.</summary>
    public double ApplicationRate =>
        ViewCount == 0 ? 0d : ApplicationCount / (double)ViewCount;

    /// <summary>
    /// Applications as a share of application-form opens. Isolates the form itself: a low
    /// figure here means the questions are the obstacle, not the job.
    /// </summary>
    public double ApplicationCompletionRate =>
        ApplicationStarts == 0 ? 0d : ApplicationCount / (double)ApplicationStarts;

    /// <summary>Hires as a share of applications.</summary>
    public double ConversionRate =>
        ApplicationCount == 0 ? 0d : HiredCount / (double)ApplicationCount;

    /// <summary>Accepted offers as a share of offers made.</summary>
    public double OfferAcceptanceRate =>
        OfferedCount == 0 ? 0d : HiredCount / (double)OfferedCount;

    /// <summary>
    /// Mean hours to a first response. Null when nothing has been answered — an unknown
    /// figure, which is not the same as a fast one and must not render as zero.
    /// </summary>
    public double? AverageResponseHours =>
        RespondedCount == 0 ? null : TotalResponseMinutes / (double)RespondedCount / 60d;

    /// <summary>Mean days from application to hire. Null until someone has been hired.</summary>
    public double? AverageTimeToHireDays =>
        HiredCount == 0 ? null : TotalTimeToHireHours / (double)HiredCount / 24d;

    /// <summary>The applicant tally by country, decoded.</summary>
    public IReadOnlyDictionary<string, int> CountryTally => Decode(ApplicantCountryTally);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation & recording
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static JobAnalytics Create(
        JobAnalyticsId id,
        JobPostingId postingId,
        EmployerProfileId employerId,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(postingId);
        ArgumentNullException.ThrowIfNull(employerId);

        return new JobAnalytics(id, postingId, employerId, now);
    }

    public void RecordImpressions(long count) =>
        SearchImpressions += Math.Max(0, count);

    public void RecordSearchClick() => SearchClicks++;

    public void RecordView(bool unique)
    {
        ViewCount++;

        if (unique)
        {
            UniqueViewCount++;
        }
    }

    /// <summary>Someone opened the application form. Not yet an application.</summary>
    public void RecordApplicationStart() => ApplicationStarts++;

    /// <summary>An application arrived, optionally tallied against the applicant's country.</summary>
    public void RecordApplication(string? countryCode = null)
    {
        ApplicationCount++;

        if (countryCode is { Length: 2 })
        {
            Tally(countryCode.ToUpperInvariant());
        }
    }

    public void RecordWithdrawal() => WithdrawnCount++;

    public void RecordShortlisted() => ShortlistedCount++;

    public void RecordInterviewed() => InterviewedCount++;

    public void RecordOffered() => OfferedCount++;

    public void RecordRejected() => RejectedCount++;

    public void RecordSaveDelta(int delta) => SaveCount = Math.Max(0, SaveCount + delta);

    public void RecordShare() => ShareCount++;

    /// <summary>
    /// Records how long the employer took to answer an application for the first time.
    /// Negative intervals — a clock skew, a backfill — are dropped rather than trusted.
    /// </summary>
    public void RecordFirstResponse(TimeSpan elapsed)
    {
        if (elapsed < TimeSpan.Zero)
        {
            return;
        }

        RespondedCount++;
        TotalResponseMinutes += (long)elapsed.TotalMinutes;
    }

    /// <summary>Records a hire and how long it took from application to acceptance.</summary>
    public void RecordHire(TimeSpan timeToHire)
    {
        HiredCount++;

        if (timeToHire > TimeSpan.Zero)
        {
            TotalTimeToHireHours += (long)timeToHire.TotalHours;
        }
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals — the country tally codec
    // ════════════════════════════════════════════════════════════════════════════════════════

    private const char PairSeparator = ';';
    private const char ValueSeparator = ':';

    /// <summary>
    /// The most countries a single posting's tally will track. Past this the tally stops
    /// growing rather than the column growing without bound — a posting drawing applicants from
    /// more than sixty countries has told the employer what they needed to know.
    /// </summary>
    private const int MaxTrackedCountries = 60;

    private void Tally(string countryCode)
    {
        var tally = new Dictionary<string, int>(Decode(ApplicantCountryTally), StringComparer.Ordinal);

        if (!tally.ContainsKey(countryCode) && tally.Count >= MaxTrackedCountries)
        {
            return;
        }

        tally[countryCode] = tally.GetValueOrDefault(countryCode) + 1;

        ApplicantCountryTally = string.Join(
            PairSeparator,
            tally.OrderByDescending(entry => entry.Value)
                .Select(entry => $"{entry.Key}{ValueSeparator}{entry.Value}"));
    }

    private static Dictionary<string, int> Decode(string? encoded)
    {
        var tally = new Dictionary<string, int>(StringComparer.Ordinal);

        if (string.IsNullOrEmpty(encoded))
        {
            return tally;
        }

        foreach (var pair in encoded.Split(PairSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var split = pair.IndexOf(ValueSeparator, StringComparison.Ordinal);

            if (split <= 0
                || !int.TryParse(
                    pair[(split + 1)..],
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out var count))
            {
                continue;
            }

            tally[pair[..split]] = count;
        }

        return tally;
    }
}
