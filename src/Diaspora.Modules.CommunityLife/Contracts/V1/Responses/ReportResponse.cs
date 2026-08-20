namespace Diaspora.Modules.CommunityLife.Contracts.V1.Responses;

/// <summary>One member's report about an event or an organization.</summary>
public sealed record ReportResponse
{
    public required Guid ReportId { get; init; }

    public required string SubjectType { get; init; }

    public required Guid SubjectId { get; init; }

    public required string Reason { get; init; }

    public string? Detail { get; init; }

    public required string Status { get; init; }

    public string? ResolutionNote { get; init; }

    public Guid? MergedIntoSubjectId { get; init; }

    /// <summary>
    /// Who filed it. Present only on the moderator's own view of the queue and never on any surface
    /// the reported party can reach.
    /// </summary>
    public Guid? ReportedByUserId { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? ResolvedAt { get; init; }
}
