namespace Diaspora.Modules.Analytics.Contracts.V1.Responses;

/// <summary>
/// The window a figure covers, echoed back.
/// <para>
/// Echoed rather than assumed, because the client fingerprints a custom range separately from a
/// named period covering the same days — the server may bucket them differently and the response is
/// how it says which it did.
/// </para>
/// </summary>
public sealed record AnalyticsRangeResponse
{
    /// <summary><c>today</c>, <c>7d</c>, <c>30d</c>, <c>90d</c>, <c>year</c> or <c>custom</c>.</summary>
    public required string Period { get; init; }

    /// <summary>Inclusive, <c>YYYY-MM-DD</c>, in <see cref="Timezone"/>.</summary>
    public required string From { get; init; }

    /// <summary>Inclusive, <c>YYYY-MM-DD</c>, in <see cref="Timezone"/>.</summary>
    public required string To { get; init; }

    /// <summary>
    /// The IANA zone the days were cut in.
    /// <para>
    /// Served on every response because §58 forbids a client silently using the device's zone. A
    /// dashboard that does not know which calendar its "1–8 August" refers to is showing a different
    /// week to a reader in Auckland than to one in Los Angeles.
    /// </para>
    /// </summary>
    public required string Timezone { get; init; }
}

/// <summary>
/// One figure, with everything needed to render it honestly.
/// <para>
/// The Phase 7 <c>CreatorMetric</c> shape exactly. <see cref="Definition"/> is the platform's own
/// sentence — §14: "Do not invent metric definitions" — and the client shows it rather than shipping
/// a glossary that drifts from what is measured.
/// </para>
/// </summary>
public sealed record CreatorMetricResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required double Value { get; init; }

    /// <summary><c>count</c>, <c>percentage</c>, <c>currency</c> or <c>duration</c>.</summary>
    public required string Format { get; init; }

    /// <summary>
    /// Change against the comparable previous window, as a percentage.
    /// <para>
    /// <b>Null is a real answer and the client renders it as one.</b> A metric whose baseline was
    /// zero has no percentage change — the honest statements are "no activity to compare against"
    /// and "not enough history", and both are unrepresentable if a null is turned into a zero.
    /// <see cref="ComparisonUnavailableReason"/> says which.
    /// </para>
    /// </summary>
    public double? ChangePercentage { get; init; }

    /// <summary>The baseline figure this was compared against, when there was one.</summary>
    public double? PreviousValue { get; init; }

    /// <summary>
    /// <c>insufficientHistory</c>, <c>zeroBaseline</c> or <c>unsupported</c>. Null when a comparison
    /// was made.
    /// </summary>
    public string? ComparisonUnavailableReason { get; init; }

    public required bool HigherIsBetter { get; init; }

    public required string Definition { get; init; }

    /// <summary>
    /// Whether this deployment measures the metric at all.
    /// <para>
    /// <b>False is not the same as a zero, and this field is the difference.</b> Reach is not
    /// measured here; a reach tile reading zero would tell a creator nobody saw their work, which is
    /// a claim this platform cannot make. The client renders an unavailable metric as its definition
    /// rather than as a number.
    /// </para>
    /// </summary>
    public required bool IsAvailable { get; init; }
}

/// <summary>One line on a chart. Buckets are cut by the server; the client never regroups.</summary>
public sealed record CreatorMetricSeriesResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary><c>hour</c>, <c>day</c>, <c>week</c> or <c>month</c>.</summary>
    public required string Granularity { get; init; }

    public required IReadOnlyList<CreatorSeriesPointResponse> Points { get; init; }
}

public sealed record CreatorSeriesPointResponse
{
    /// <summary>Bucket start. <c>YYYY-MM-DD</c> for day and coarser; an ISO instant for <c>hour</c>.</summary>
    public required string At { get; init; }

    public required double Value { get; init; }

    /// <summary>
    /// True where the bucket has not finished.
    /// <para>
    /// Today's bucket, and the last bucket of a weekly or monthly series, are almost always partial.
    /// A chart that drew them like the others would show a collapse at the right-hand edge on every
    /// dashboard on the platform, every day.
    /// </para>
    /// </summary>
    public required bool IsPartialBucket { get; init; }
}

/// <summary>
/// A creator's overview for a period.
/// <para>The Phase 7 <c>CreatorAnalyticsSummary</c> shape, plus the freshness fields §47 requires.</para>
/// </summary>
public sealed record CreatorAnalyticsSummaryResponse
{
    public required AnalyticsRangeResponse Range { get; init; }

    /// <summary>The baseline window the comparisons were made against.</summary>
    public required AnalyticsRangeResponse ComparisonRange { get; init; }

    public required IReadOnlyList<CreatorMetricResponse> Metrics { get; init; }

    public required IReadOnlyList<CreatorMetricSeriesResponse> Series { get; init; }

    /// <summary>When this response was built.</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>
    /// When the underlying rollups were last recomputed.
    /// <para>
    /// <b>A different question from <see cref="GeneratedAt"/>, and §13 is explicit that conflating
    /// them is a lie.</b> A response assembled a moment ago from rollups computed twenty minutes ago
    /// is twenty minutes old, and reporting the assembly time as freshness is how "updated just now"
    /// ends up on a stale dashboard. Null before the projection has ever run.
    /// </para>
    /// </summary>
    public DateTimeOffset? DataAsOf { get; init; }

    /// <summary>
    /// The consistency model, in one word: <c>NearRealTime</c> or <c>Aggregated</c>.
    /// <para>
    /// Never <c>RealTime</c>. Analytics on this platform is a projection over a durable event
    /// stream and is eventually consistent by construction; claiming otherwise would be the §47
    /// failure.
    /// </para>
    /// </summary>
    public required string Freshness { get; init; }

    /// <summary>How stale the data can be, in the platform's own words.</summary>
    public required string FreshnessNote { get; init; }

    /// <summary>
    /// The metric definitions' version.
    /// <para>
    /// Served so a change in what a word means is visible as a version rather than as a cliff in a
    /// chart, and so an old report can state which definition it ran under.
    /// </para>
    /// </summary>
    public required string DefinitionVersion { get; init; }

    /// <summary>Metrics the caller asked for, or the headline set includes, that are not measured here.</summary>
    public required IReadOnlyList<string> UnavailableMetrics { get; init; }
}

/// <summary>One item's performance for a period.</summary>
public sealed record CreatorContentPerformanceResponse
{
    public required string ContentId { get; init; }

    /// <summary>
    /// The client's own vocabulary — <c>Guide</c>, <c>Update</c>, <c>Event</c>, <c>Marketplace</c>,
    /// <c>Housing</c>, <c>Job</c>, <c>Provider</c>, <c>Organization</c>.
    /// </summary>
    public required string ContentType { get; init; }

    public required string Title { get; init; }

    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>The producing module's own status word, echoed. Never interpreted here.</summary>
    public required string Status { get; init; }

    public required IReadOnlyList<CreatorMetricResponse> Metrics { get; init; }

    /// <summary>
    /// Whether the item currently carries a paid placement.
    /// <para>
    /// <b>Always false on this deployment, and that is a stated absence rather than a default.</b>
    /// Whether a placement is active is B15's, it is not carried on any contract this module
    /// consumes, and inferring it from anything here would be a guess about somebody's spend. The
    /// field is served because the client's type requires it; the report records the dependency.
    /// </para>
    /// </summary>
    public required bool IsSponsored { get; init; }
}

/// <summary>A cursor-paged page of content performance.</summary>
public sealed record CreatorContentPerformancePageResponse
{
    public required IReadOnlyList<CreatorContentPerformanceResponse> Items { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }
}
