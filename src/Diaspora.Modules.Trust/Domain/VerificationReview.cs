using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// One reviewer's recorded decision on a verification request.
/// <para>
/// Append-only. A review is never edited or deleted — if a reviewer changes their mind, a
/// second review is added and the request's status follows the latest. Verification is one
/// of the few places on the platform where "who decided this, when, and on what evidence"
/// has to survive the decision being reversed.
/// </para>
/// </summary>
public sealed class VerificationReview : Entity<VerificationReviewId>
{
    public const int MaxNotesLength = 2000;
    public const int MaxReviewerNameLength = 200;

    public VerificationRequestId RequestId { get; private set; } = default!;

    /// <summary>
    /// Who decided. Null when a rule decided — <see cref="DecisionMode"/> says which, and
    /// <see cref="DecidedByName"/> then carries the rule's name.
    /// </summary>
    public UserId? ReviewerId { get; private set; }

    /// <summary>Reviewer identifier, or the name of the automated policy that decided.</summary>
    public string? DecidedByName { get; private set; }

    public VerificationStatus FromStatus { get; private set; }

    public VerificationStatus ToStatus { get; private set; }

    public VerificationDecisionMode DecisionMode { get; private set; }

    public VerificationRejectionReason? RejectionReason { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset DecidedAt { get; private set; }

    private VerificationReview() { }

    private VerificationReview(
        VerificationReviewId id,
        VerificationRequestId requestId,
        VerificationStatus fromStatus,
        VerificationStatus toStatus,
        VerificationDecisionMode decisionMode,
        DateTimeOffset decidedAt) : base(id)
    {
        RequestId = requestId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        DecisionMode = decisionMode;
        DecidedAt = decidedAt;
    }

    internal static VerificationReview ByReviewer(
        VerificationRequestId requestId,
        UserId reviewerId,
        string? reviewerName,
        VerificationStatus fromStatus,
        VerificationStatus toStatus,
        VerificationRejectionReason? rejectionReason,
        string? notes,
        DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(reviewerId);

        return new VerificationReview(
            VerificationReviewId.New(), requestId, fromStatus, toStatus,
            VerificationDecisionMode.ManualReview, decidedAt)
        {
            ReviewerId = reviewerId,
            DecidedByName = Truncate(reviewerName, MaxReviewerNameLength),
            RejectionReason = rejectionReason,
            Notes = Truncate(notes, MaxNotesLength)
        };
    }

    internal static VerificationReview ByRule(
        VerificationRequestId requestId,
        string ruleName,
        VerificationStatus fromStatus,
        VerificationStatus toStatus,
        VerificationDecisionMode decisionMode,
        VerificationRejectionReason? rejectionReason,
        string? notes,
        DateTimeOffset decidedAt)
    {
        ArgumentNullException.ThrowIfNull(requestId);

        return new VerificationReview(
            VerificationReviewId.New(), requestId, fromStatus, toStatus, decisionMode, decidedAt)
        {
            DecidedByName = Truncate(
                Guard.Against.NullOrWhiteSpace(ruleName, nameof(ruleName)), MaxReviewerNameLength),
            RejectionReason = rejectionReason,
            Notes = Truncate(notes, MaxNotesLength)
        };
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
