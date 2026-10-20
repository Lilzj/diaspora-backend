namespace Diaspora.Modules.Analytics.Contracts.V1.Requests;

/// <summary>
/// The client's <c>AnalyticsQuery</c>: what a dashboard, a saved report or an export asks for.
/// <para>
/// One shape in and out. A saved report and an export both keep the query they were made from and
/// hand it back unchanged, so the client's report builder can reopen exactly what it saved.
/// </para>
/// </summary>
public sealed record AnalyticsQueryPayload
{
    public AnalyticsSubjectPayload? Subject { get; init; }

    /// <summary>Metric keys. Empty means the subject's headline set.</summary>
    public IReadOnlyList<string>? Metrics { get; init; }

    public AnalyticsRangePayload? Range { get; init; }

    public string? Granularity { get; init; }

    public string? Comparison { get; init; }

    public IReadOnlyList<AnalyticsFilterPayload>? Filters { get; init; }

    public IReadOnlyList<string>? Breakdowns { get; init; }

    public bool? IncludeSeries { get; init; }

    public string? CommunityScopeToken { get; init; }
}

/// <summary>The client's <c>AnalyticsSubject</c>.</summary>
public sealed record AnalyticsSubjectPayload
{
    public string? Type { get; init; }

    public string? Id { get; init; }

    public string? Label { get; init; }
}

/// <summary>The client's <c>AnalyticsTimeRange</c>.</summary>
public sealed record AnalyticsRangePayload
{
    public string? Preset { get; init; }

    public string? From { get; init; }

    public string? To { get; init; }

    public string? Timezone { get; init; }

    public bool? IsDeviceTimezone { get; init; }
}

/// <summary>The client's <c>AnalyticsFilter</c>.</summary>
public sealed record AnalyticsFilterPayload
{
    public string? Dimension { get; init; }

    public IReadOnlyList<string>? Values { get; init; }
}

/// <summary>Body of <c>POST /analytics/reports</c> — the client's <c>SaveReportInput</c>.</summary>
public sealed record CreateAnalyticsReportRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public AnalyticsQueryPayload? Query { get; init; }

    public string? Visualization { get; init; }

    public string? Sharing { get; init; }
}

/// <summary>
/// Body of <c>PUT /analytics/reports/{reportId}</c> — partial: an absent field is left as it was.
/// </summary>
public sealed record UpdateAnalyticsReportRequest
{
    public string? Name { get; init; }

    public string? Description { get; init; }

    public AnalyticsQueryPayload? Query { get; init; }

    public string? Visualization { get; init; }

    public string? Sharing { get; init; }

    public bool? IsFavorite { get; init; }
}

/// <summary>Body of <c>POST /analytics/exports</c>.</summary>
public sealed record RequestAnalyticsExportRequest
{
    public AnalyticsQueryPayload? Query { get; init; }

    /// <summary><c>csv</c>. <c>pdf</c> and <c>json</c> are refused with a reason.</summary>
    public string? Format { get; init; }
}
