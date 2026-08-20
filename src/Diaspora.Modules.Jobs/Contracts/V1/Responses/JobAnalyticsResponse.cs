namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One posting's measured performance — the employer's funnel.
/// <para>
/// <b>Every rate is nullable and every null means "not yet knowable".</b> An employer with two
/// applications has no meaningful conversion rate, and rendering one as <c>0%</c> would be the
/// platform inventing a fact. Null lets the client say "not enough data yet", which is true.
/// </para>
/// <para>
/// <see cref="AverageResponseHours"/> is the number that changes behaviour. It is on the
/// employer's own dashboard as well as their public profile, because an employer who cannot see
/// they are keeping people waiting cannot stop.
/// </para>
/// </summary>
public sealed record JobAnalyticsResponse
{
    public required Guid JobId { get; init; }

    // ── The funnel, top to bottom ───────────────────────────────────────────────────────────

    public required long SearchImpressions { get; init; }

    public required long SearchClicks { get; init; }

    public required long ViewCount { get; init; }

    public required long UniqueViewCount { get; init; }

    /// <summary>Application forms opened. The intent signal, before the form is finished.</summary>
    public required long ApplicationStarts { get; init; }

    public required int ApplicationCount { get; init; }

    public required int WithdrawnCount { get; init; }

    public required int ShortlistedCount { get; init; }

    public required int InterviewedCount { get; init; }

    public required int OfferedCount { get; init; }

    public required int HiredCount { get; init; }

    public required int RejectedCount { get; init; }

    public required int SaveCount { get; init; }

    // ── Derived ─────────────────────────────────────────────────────────────────────────────

    /// <summary>Opens as a share of appearances. Measures the card, not the job.</summary>
    public required double ClickThroughRate { get; init; }

    /// <summary>Applications as a share of views.</summary>
    public required double ApplicationRate { get; init; }

    /// <summary>
    /// Applications as a share of forms opened. Isolates the form: a low figure here means the
    /// screening questions are the obstacle, not the job.
    /// </summary>
    public required double ApplicationCompletionRate { get; init; }

    public required double ConversionRate { get; init; }

    public required double OfferAcceptanceRate { get; init; }

    /// <summary>Null until something has been answered. Not the same as zero.</summary>
    public double? AverageResponseHours { get; init; }

    /// <summary>Null until somebody has been hired.</summary>
    public double? AverageTimeToHireDays { get; init; }

    /// <summary>Applicants by country. The geographic distribution the brief asks for.</summary>
    public required IReadOnlyDictionary<string, int> ApplicantsByCountry { get; init; }
}
