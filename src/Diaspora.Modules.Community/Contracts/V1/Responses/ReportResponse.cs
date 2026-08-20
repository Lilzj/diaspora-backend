namespace Diaspora.Modules.Community.Contracts.V1.Responses;

/// <summary>A filed report. Field names taken from the client's content report.</summary>
public sealed record ReportResponse
{
    public required Guid ReportId { get; init; }

    public required Guid ReporterId { get; init; }

    public string? ReporterName { get; init; }

    public required string ContentType { get; init; }

    public required Guid ContentId { get; init; }

    public required string ReasonCode { get; init; }

    public string? Description { get; init; }

    public string? EvidenceUrl { get; init; }

    public required string Status { get; init; }

    public Guid? ResolvedBy { get; init; }

    public string? ResolutionNote { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }
}
