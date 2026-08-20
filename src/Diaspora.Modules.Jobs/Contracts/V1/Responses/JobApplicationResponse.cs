namespace Diaspora.Modules.Jobs.Contracts.V1.Responses;

/// <summary>
/// One application in full.
/// <para>
/// <b>The same record renders for both parties, but not identically.</b> The mapper decides which
/// history entries to include from <c>IsVisibleToCandidate</c>, so a candidate reading their own
/// application sees their timeline and an employer reading it sees theirs, internal notes
/// included. One shape, two audiences, and the filtering happens where the rule lives rather
/// than in a client.
/// </para>
/// </summary>
public sealed record JobApplicationResponse
{
    public required Guid ApplicationId { get; init; }

    public required Guid JobId { get; init; }

    public required string JobTitle { get; init; }

    public required string JobSlug { get; init; }

    public required Guid EmployerId { get; init; }

    public required string EmployerName { get; init; }

    public required Guid CandidateId { get; init; }

    public required string Status { get; init; }

    /// <summary>The employer's own pipeline stage. Their vocabulary, not the platform's.</summary>
    public string? StageKey { get; init; }

    public string? CoverLetter { get; init; }

    public Guid? ResumeId { get; init; }

    public string? ResumeLabel { get; init; }

    public required IReadOnlyList<ApplicationAnswerResponse> Answers { get; init; }

    public required IReadOnlyList<ApplicationAttachmentResponse> Attachments { get; init; }

    /// <summary>Filtered for the reader. See the record summary.</summary>
    public required IReadOnlyList<ApplicationHistoryResponse> History { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }

    public DateTimeOffset? FirstViewedAt { get; init; }

    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>Why it ended, when the employer gave something the candidate may see.</summary>
    public string? DecisionReason { get; init; }

    /// <summary>How long the candidate waited for a first response. Null while still waiting —
    /// which is not zero and must never render as "answered immediately".</summary>
    public double? HoursToFirstResponse { get; init; }

    public required bool IsActive { get; init; }

    public required bool CanWithdraw { get; init; }

    public required int Revision { get; init; }
}
