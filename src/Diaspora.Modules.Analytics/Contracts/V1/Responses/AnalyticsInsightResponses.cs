namespace Diaspora.Modules.Analytics.Contracts.V1.Responses;

/// <summary>
/// One retention cohort — the client's <c>AnalyticsRetentionCohort</c>.
/// <para>
/// <b>A null is "not measured yet", never zero.</b> An offset whose period has not begun carries
/// null, so a week that has not happened cannot render as a week everybody left in.
/// </para>
/// </summary>
public sealed record AnalyticsRetentionCohortResponse
{
    /// <summary><c>YYYY-MM-DD</c>: the first day of the period the cohort was first seen in.</summary>
    public required string CohortDate { get; init; }

    public required long CohortSize { get; init; }

    /// <summary>Retained share at each offset, 0–1. Index 0 is the period the cohort was first seen in.</summary>
    public required IReadOnlyList<double?> Values { get; init; }

    /// <summary>True while the cohort has not yet aged through its last offset.</summary>
    public required bool IsIncomplete { get; init; }
}

/// <summary>A cohort that was asked for and cannot be counted, with the reason.</summary>
public sealed record AnalyticsUnavailableCohortResponse
{
    public required string CohortDate { get; init; }

    public required string Reason { get; init; }
}

/// <summary>
/// A cohort retention table — the client's <c>AnalyticsRetentionTable</c>, plus what it cannot count.
/// </summary>
public sealed record AnalyticsRetentionTableResponse
{
    public required AnalyticsSubjectResponse Subject { get; init; }

    /// <summary><c>day</c>, <c>week</c> or <c>month</c>.</summary>
    public required string Granularity { get; init; }

    public required IReadOnlyList<string> OffsetLabels { get; init; }

    /// <summary>What "retained" counts as, in the platform's own words.</summary>
    public required string MeasurementWindow { get; init; }

    /// <summary>The time zone days are cut in. Always the platform's reporting zone.</summary>
    public required string Timezone { get; init; }

    /// <summary>Oldest first.</summary>
    public required IReadOnlyList<AnalyticsRetentionCohortResponse> Cohorts { get; init; }

    /// <summary>Cohorts asked for whose activity is no longer kept. Never filled with an estimate.</summary>
    public required IReadOnlyList<AnalyticsUnavailableCohortResponse> UnavailableCohorts { get; init; }

    public required string Source { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}

/// <summary>
/// A statement the platform makes about a subject's figures — the client's <c>AnalyticsInsight</c>.
/// <para>
/// Every one is arithmetic over counted figures, stated with the figures it rests on in
/// <see cref="Detail"/>. None is a prediction, a score or a guess about why.
/// </para>
/// </summary>
public sealed record AnalyticsInsightResponse
{
    public required string Key { get; init; }

    public required string Headline { get; init; }

    public string? Detail { get; init; }

    /// <summary><c>positive</c>, <c>negative</c> or <c>neutral</c>.</summary>
    public required string Tone { get; init; }

    public AnalyticsInsightActionResponse? Action { get; init; }

    public required string Source { get; init; }
}

public sealed record AnalyticsInsightActionResponse
{
    public required string Label { get; init; }

    public required string Route { get; init; }
}

/// <summary>A part of a response the platform cannot produce, and why.</summary>
public sealed record AnalyticsUnavailablePartResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Reason { get; init; }
}

// ── Premium summary — MON-GAP-010, the client's `PremiumAnalytics` ────────────────────────────

/// <summary>One headline figure in the Phase 6 <c>PerformanceMetric</c> shape.</summary>
public sealed record PremiumAnalyticsMetricResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required double Value { get; init; }

    /// <summary><c>count</c>. Every figure in the summary is a count.</summary>
    public required string Unit { get; init; }

    /// <summary>Against the preceding period of equal length, as a ratio. Null where that had none.</summary>
    public double? ChangeRatio { get; init; }

    public required string Definition { get; init; }
}

public sealed record PremiumAnalyticsBucketResponse
{
    public required string Label { get; init; }

    public required double Value { get; init; }

    public required double Ratio { get; init; }
}

public sealed record PremiumAnalyticsBreakdownResponse
{
    public required string Dimension { get; init; }

    public required string Label { get; init; }

    public required IReadOnlyList<PremiumAnalyticsBucketResponse> Buckets { get; init; }
}

public sealed record PremiumAnalyticsPointResponse
{
    /// <summary><c>YYYY-MM-DD</c>, a reporting day.</summary>
    public required string Date { get; init; }

    public required long Views { get; init; }

    public required long Engagements { get; init; }
}

public sealed record PremiumAnalyticsSubjectResponse
{
    public required string SubjectType { get; init; }

    public required string SubjectId { get; init; }

    public required string Title { get; init; }

    public required long Views { get; init; }

    public required long Engagements { get; init; }
}

/// <summary>
/// The advanced summary the <c>analytics.advanced</c> entitlement unlocks: the caller's own work,
/// across everything they publish.
/// </summary>
public sealed record PremiumAnalyticsSummaryResponse
{
    public required DateTimeOffset PeriodStart { get; init; }

    public required DateTimeOffset PeriodEnd { get; init; }

    public required string Timezone { get; init; }

    public required IReadOnlyList<PremiumAnalyticsMetricResponse> Headline { get; init; }

    public required IReadOnlyList<PremiumAnalyticsBreakdownResponse> Breakdowns { get; init; }

    /// <summary>Every reporting day in the period, oldest first, including the quiet ones.</summary>
    public required IReadOnlyList<PremiumAnalyticsPointResponse> Series { get; init; }

    /// <summary>The caller's most-viewed items in the period, most first.</summary>
    public required IReadOnlyList<PremiumAnalyticsSubjectResponse> TopSubjects { get; init; }

    /// <summary>How far behind live the figures may be: the projection's interval, in hours.</summary>
    public required double ReportingLagHours { get; init; }

    /// <summary>Breakdowns the summary cannot produce, and why.</summary>
    public required IReadOnlyList<AnalyticsUnavailablePartResponse> Unavailable { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}
