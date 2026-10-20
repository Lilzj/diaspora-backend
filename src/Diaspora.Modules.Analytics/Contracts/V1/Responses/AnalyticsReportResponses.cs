using Diaspora.Modules.Analytics.Contracts.V1.Requests;

namespace Diaspora.Modules.Analytics.Contracts.V1.Responses;

/// <summary>A saved report — the client's <c>AnalyticsReport</c>.</summary>
public sealed record AnalyticsReportResponse
{
    public required string ReportId { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required AnalyticsQueryPayload Query { get; init; }

    public required string Visualization { get; init; }

    /// <summary>Opaque. Compared by the client, never displayed.</summary>
    public required string OwnerId { get; init; }

    public required bool IsOwnedByCaller { get; init; }

    public required bool IsFavorite { get; init; }

    /// <summary>Always <c>private</c> on this deployment.</summary>
    public required string Sharing { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public required DateTimeOffset UpdatedAt { get; init; }

    /// <summary>Not recorded: running a report is a dataset read, and reads write nothing.</summary>
    public DateTimeOffset? LastRunAt { get; init; }
}

/// <summary>What <c>DELETE /analytics/reports/{reportId}</c> answers.</summary>
public sealed record AnalyticsReportDeletedResponse
{
    public required string ReportId { get; init; }
}

/// <summary>An export job — the client's <c>AnalyticsExport</c>.</summary>
public sealed record AnalyticsExportResponse
{
    public required string ExportId { get; init; }

    public required string Format { get; init; }

    /// <summary><c>queued</c>, <c>ready</c>, <c>failed</c> or <c>expired</c>.</summary>
    public required string Status { get; init; }

    public required AnalyticsQueryPayload Query { get; init; }

    public required DateTimeOffset RequestedAt { get; init; }

    public DateTimeOffset? CompletedAt { get; init; }

    /// <summary>
    /// Where the file is, while it is ready: a path on this API carrying a per-export token, so it
    /// opens without a session. Absent before ready and after expiry.
    /// </summary>
    public string? DownloadUrl { get; init; }

    public long? SizeBytes { get; init; }

    public string? FailureReason { get; init; }

    public DateTimeOffset? ExpiresAt { get; init; }
}

/// <summary>The file an export produced.</summary>
public sealed record AnalyticsExportFile(string FileName, string ContentType, byte[] Bytes);

/// <summary>One step of a funnel — the client's <c>AnalyticsFunnelStep</c>.</summary>
public sealed record AnalyticsFunnelStepResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary>Journeys that reached this step having reached every earlier one.</summary>
    public required long Count { get; init; }

    /// <summary>A ratio against the previous step. Null on the first step and after a zero.</summary>
    public double? ConversionFromPrevious { get; init; }

    /// <summary>A ratio against the first step. Null when the first step is zero.</summary>
    public double? ConversionFromStart { get; init; }

    public double? DropOffFromPrevious { get; init; }
}

/// <summary>A step this platform does not record, named so the client can say so rather than draw a zero.</summary>
public sealed record AnalyticsFunnelUnavailableStepResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required string Reason { get; init; }
}

/// <summary>A funnel — the client's <c>AnalyticsFunnel</c>, plus the steps it could not count.</summary>
public sealed record AnalyticsFunnelResponse
{
    public required string Key { get; init; }

    public required string Label { get; init; }

    public required AnalyticsSubjectResponse Subject { get; init; }

    public required IReadOnlyList<AnalyticsFunnelStepResponse> Steps { get; init; }

    /// <summary>Steps the client's journey names that nothing on this platform records.</summary>
    public required IReadOnlyList<AnalyticsFunnelUnavailableStepResponse> UnavailableSteps { get; init; }

    public required AnalyticsTimeRangeResponse Range { get; init; }

    /// <summary>Always <c>backend</c>.</summary>
    public required string Source { get; init; }

    /// <summary>Always <c>server-aggregate</c>.</summary>
    public required string Computation { get; init; }

    public required DataFreshnessResponse Freshness { get; init; }
}
