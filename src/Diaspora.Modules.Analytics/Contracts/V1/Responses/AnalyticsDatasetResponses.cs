namespace Diaspora.Modules.Analytics.Contracts.V1.Responses;

/// <summary>What a dataset is about. The Phase 15 <c>AnalyticsSubject</c>.</summary>
public sealed record AnalyticsSubjectResponse
{
    /// <summary><c>creator</c>, <c>guide</c>, <c>update</c>, <c>event</c>, <c>job</c>, …</summary>
    public required string Type { get; init; }

    /// <summary>Absent for a self-scoped subject, which is the creator's whole point.</summary>
    public string? Id { get; init; }

    public string? Label { get; init; }
}

/// <summary>
/// A resolved range in the Phase 15 wire format.
/// <para>
/// Three fields rather than one instant, and <see cref="IsDeviceTimezone"/> is always false from
/// this platform: the server publishes a reporting timezone, so a client never has to fall back to
/// the device's and pretend the two are the same.
/// </para>
/// </summary>
public sealed record AnalyticsTimeRangeResponse
{
    /// <summary>The Phase 15 preset — <c>today</c>, <c>last7d</c>, <c>last30d</c>, … or <c>custom</c>.</summary>
    public required string Preset { get; init; }

    public required string From { get; init; }

    public required string To { get; init; }

    public required string Timezone { get; init; }

    public required bool IsDeviceTimezone { get; init; }
}

/// <summary>Provenance: who produced a value. The Phase 15 <c>Sourced&lt;number&gt;</c>.</summary>
public sealed record SourcedValueResponse
{
    public required double Value { get; init; }

    /// <summary>Always <c>backend</c> from this platform. There is no simulated arm on the server.</summary>
    public required string Source { get; init; }
}

/// <summary>
/// When a figure was computed and who computed it. The Phase 15 <c>DataFreshness</c>.
/// <para>
/// Required on every metric rather than optional, because §36 asks every dashboard to communicate
/// freshness and an optional field is one somebody forgets.
/// </para>
/// </summary>
public sealed record DataFreshnessResponse
{
    public required DateTimeOffset ComputedAt { get; init; }

    public required string SourceName { get; init; }

    public string? Methodology { get; init; }

    /// <summary>What the figure cannot tell you. Rendered verbatim beside it.</summary>
    public IReadOnlyList<string>? Limitations { get; init; }
}

/// <summary>A change against a baseline. Null percentages always carry a reason.</summary>
public sealed record AnalyticsComparisonResponse
{
    public required string Basis { get; init; }

    public string? BaselineFrom { get; init; }

    public string? BaselineTo { get; init; }

    public double? BaselineValue { get; init; }

    /// <summary>
    /// A ratio, not a percentage.
    /// <para>
    /// <b>0.18 means eighteen percent.</b> The Phase 15 model's convention, and the client's adapter
    /// divides Phase 7's percentage by a hundred to reach it — a hundredfold error that looks
    /// plausible in both directions, which is why the two shapes are served separately rather than
    /// one being derived from the other on the wire.
    /// </para>
    /// </summary>
    public double? ChangePercent { get; init; }

    public double? ChangeAbsolute { get; init; }

    /// <summary><c>insufficientHistory</c>, <c>zeroBaseline</c>, <c>unsupported</c> or <c>suppressed</c>.</summary>
    public string? UnavailableReason { get; init; }
}

/// <summary>One metric in the shared Phase 15 shape.</summary>
public sealed record AnalyticsMetricResponse
{
    /// <summary>The dotted catalogue key — <c>content.views</c>, <c>content.engagementRate</c>.</summary>
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required SourcedValueResponse Value { get; init; }

    public required string Format { get; init; }

    /// <summary><c>server-aggregate</c> or <c>estimated</c>. Never <c>client-derived</c> from a server.</summary>
    public required string Computation { get; init; }

    public required bool HigherIsBetter { get; init; }

    public AnalyticsComparisonResponse? Comparison { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }

    /// <summary><c>public</c>, <c>owner</c>, <c>administrative</c> or <c>financial</c>.</summary>
    public required string PrivacyClass { get; init; }

    /// <summary>
    /// True where the underlying population was too small to publish.
    /// <para>
    /// §47: a count over three people is three people's behaviour. Suppression happens before the
    /// response is built — by the time a figure reaches a device it has already been disclosed —
    /// and the flag is what lets the client render "too few to report" rather than a zero.
    /// </para>
    /// </summary>
    public bool IsSuppressed { get; init; }
}

public sealed record AnalyticsPointResponse
{
    public required string At { get; init; }

    public required double Value { get; init; }

    public double? BaselineValue { get; init; }

    public bool IsPartialBucket { get; init; }
}

public sealed record AnalyticsSeriesResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Granularity { get; init; }

    public required string Format { get; init; }

    public required IReadOnlyList<AnalyticsPointResponse> Points { get; init; }

    public required string Source { get; init; }
}

/// <summary>A thing a leaderboard row points at. Names things, never people.</summary>
public sealed record AnalyticsEntityRefResponse
{
    public required string EntityType { get; init; }

    public required string EntityId { get; init; }

    public required string Title { get; init; }

    public string? Route { get; init; }
}

public sealed record AnalyticsLeaderboardRowResponse
{
    /// <summary>The server's ordering, echoed. The client never sorts a leaderboard.</summary>
    public required int Position { get; init; }

    public required AnalyticsEntityRefResponse Entity { get; init; }

    public required double Value { get; init; }

    public required string Format { get; init; }

    public double? SecondaryValue { get; init; }

    public string? SecondaryFormat { get; init; }

    public string? SecondaryLabel { get; init; }
}

public sealed record AnalyticsLeaderboardResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string MetricKey { get; init; }

    public required IReadOnlyList<AnalyticsLeaderboardRowResponse> Rows { get; init; }

    public string? NextCursor { get; init; }

    public required bool HasMore { get; init; }

    public required string Source { get; init; }
}

/// <summary>
/// Everything a Phase 15 dashboard renders, in one object.
/// <para>
/// <b>The empty collections are served rather than omitted, and they are not filler.</b> A dataset
/// with no funnels is a dataset this deployment computes no funnel for; the client's type requires
/// the array and an absent one would be indistinguishable from a serialization fault. What the
/// dataset does <em>not</em> do is invent a funnel, a retention grid or an insight — those are
/// FRONTEND-PHASE15-GAP-016, -004 and -028, they belong to later phases, and §59 forbids filling
/// them with anything.
/// </para>
/// </summary>
public sealed record AnalyticsDatasetResponse
{
    public required AnalyticsSubjectResponse Subject { get; init; }

    public required AnalyticsTimeRangeResponse Range { get; init; }

    public required string Comparison { get; init; }

    /// <summary><c>LIVE</c>, <c>PARTIAL</c>, <c>ESTIMATED</c> or <c>UNAVAILABLE</c>.</summary>
    public required string Status { get; init; }

    public required IReadOnlyList<AnalyticsMetricResponse> Metrics { get; init; }

    public required IReadOnlyList<AnalyticsSeriesResponse> Series { get; init; }

    public required IReadOnlyList<object> Breakdowns { get; init; }

    public required IReadOnlyList<object> Funnels { get; init; }

    public required IReadOnlyList<AnalyticsLeaderboardResponse> Leaderboards { get; init; }

    public required IReadOnlyList<object> Insights { get; init; }

    /// <summary>The oldest of the dataset's parts, never the newest.</summary>
    public required DataFreshnessResponse Freshness { get; init; }

    public required IReadOnlyList<string> UnavailableMetrics { get; init; }
}
