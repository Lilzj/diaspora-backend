namespace Diaspora.Modules.Trust.Contracts.V1.Responses;

/// <summary>
/// A verification request, as the member sees it.
/// <para>
/// Field names match the frontend's <c>VerificationResponse</c>
/// (<c>types/verification.ts:84</c>). Enums serialise as their string names, so
/// <c>status</c> arrives as <c>"Pending"</c> and <c>rejectionReasonCode</c> as
/// <c>"EXPIRED_DOCUMENT"</c> — the literals the client already compares against.
/// </para>
/// </summary>
public sealed record VerificationResponse
{
    public required Guid VerificationId { get; init; }

    public required Guid UserId { get; init; }

    public required string Type { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }

    public DateTimeOffset? ReviewedAt { get; init; }

    /// <summary>When the grant lapses. Null means it does not.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>SCREAMING_SNAKE, matching the frontend's union.</summary>
    public string? RejectionReasonCode { get; init; }

    public string? RejectionReasonDetails { get; init; }

    public string? Nationality { get; init; }

    public string? ResidenceCountry { get; init; }

    public string? TargetCountry { get; init; }

    public DateOnly? ArrivalDate { get; init; }

    public IReadOnlyList<string> ExpertiseAreas { get; init; } = [];

    public int DocumentCount { get; init; }

    /// <summary>Whether the member may appeal this outcome right now.</summary>
    public bool CanAppeal { get; init; }
}

/// <summary>
/// A verification with its documents and review history. Returned from the detail route only.
/// <para>
/// Separate from <see cref="VerificationResponse"/> rather than nullable fields on it,
/// because the list route returning review history for fifty rows is a load nobody asked for.
/// </para>
/// </summary>
public sealed record VerificationDetailResponse
{
    public required VerificationResponse Verification { get; init; }

    public required IReadOnlyList<VerificationDocumentResponse> Documents { get; init; }

    /// <summary>
    /// Review history. Staff see every entry with reviewer identities; a member sees only
    /// the decisions that were communicated to them, with reviewers anonymised.
    /// </summary>
    public required IReadOnlyList<VerificationReviewResponse> Reviews { get; init; }

    /// <summary>
    /// The member's name as the platform knows them. Staff only — a reviewer deciding a claim
    /// about a person needs to know who the person is, and an identifier tells them nothing.
    /// Null for the member themselves, and when the name cannot be resolved.
    /// </summary>
    public string? MemberName { get; init; }

    /// <summary>
    /// A short-lived link to the selfie taken with the submission, for comparison against the
    /// document's photograph. Staff only; null when none was taken or it cannot be served.
    /// </summary>
    public string? SelfieUrl { get; init; }
}

/// <summary>
/// One attached document.
/// <para>
/// <c>documentUrl</c> is a short-lived presigned link minted per request, not a stored
/// value — an immigration document must not sit in the database as a permanently live URL.
/// It is null unless the caller may read the file.
/// </para>
/// </summary>
public sealed record VerificationDocumentResponse
{
    public required Guid DocumentId { get; init; }

    public required string DocumentType { get; init; }

    public required string Status { get; init; }

    /// <summary>Presigned, short-lived. Null when the caller may not read the file.</summary>
    public string? DocumentUrl { get; init; }

    public string? OriginalFileName { get; init; }

    public long SizeInBytes { get; init; }

    public string? DocumentNumber { get; init; }

    public string? IssuingCountry { get; init; }

    public DateOnly? IssuedOn { get; init; }

    public DateOnly? ExpiresOn { get; init; }

    public int Version { get; init; }

    public string? ReviewComment { get; init; }

    public required DateTimeOffset UploadedAt { get; init; }
}

/// <summary>One entry in a verification's review history.</summary>
public sealed record VerificationReviewResponse
{
    public required Guid ReviewId { get; init; }

    /// <summary>Reviewer name, or the automated policy's name. Null when anonymised.</summary>
    public string? DecidedBy { get; init; }

    public required string FromStatus { get; init; }

    public required string ToStatus { get; init; }

    /// <summary>ManualReview, AutomaticApproval, AutomaticRejection, RoutedToReview.</summary>
    public required string DecisionMode { get; init; }

    public string? RejectionReason { get; init; }

    /// <summary>Internal notes. Omitted from a member-facing projection.</summary>
    public string? Notes { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

/// <summary>
/// One row in the staff review queue.
/// <para>
/// Carries enough context to triage without opening each item — how long it has waited,
/// whether it is overdue, and what the submitter's standing is.
/// </para>
/// </summary>
public sealed record VerificationQueueItemResponse
{
    public required Guid VerificationId { get; init; }

    public required Guid UserId { get; init; }

    public required string Type { get; init; }

    public required string Status { get; init; }

    public required DateTimeOffset SubmittedAt { get; init; }

    public DateTimeOffset? ReviewDueAt { get; init; }

    public required bool IsOverdue { get; init; }

    public Guid? AssignedReviewerId { get; init; }

    public int DocumentCount { get; init; }

    public string? TargetCountry { get; init; }

    /// <summary>The submitter's trust tier, so a reviewer can weigh the claim.</summary>
    public string? SubmitterTier { get; init; }

    public int SubmitterViolationCount { get; init; }
}
