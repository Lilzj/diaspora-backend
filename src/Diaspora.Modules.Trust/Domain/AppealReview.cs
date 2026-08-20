using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// One step in an appeal's review history.
/// <para>
/// Append-only, like <see cref="VerificationReview"/>. An appeal that was escalated and then
/// dismissed leaves two rows, and the member is entitled to see both — an appeals trail that
/// only records the final answer cannot be audited for fairness.
/// </para>
/// </summary>
public sealed class AppealReview : Entity<AppealReviewId>
{
    public const int MaxNotesLength = 2000;

    public AppealId AppealId { get; private set; } = default!;

    public UserId ReviewerId { get; private set; } = default!;

    public AppealStatus FromStatus { get; private set; }

    public AppealStatus ToStatus { get; private set; }

    public string? Notes { get; private set; }

    public DateTimeOffset ReviewedAt { get; private set; }

    private AppealReview() { }

    private AppealReview(
        AppealReviewId id,
        AppealId appealId,
        UserId reviewerId,
        AppealStatus fromStatus,
        AppealStatus toStatus,
        string? notes,
        DateTimeOffset reviewedAt) : base(id)
    {
        AppealId = appealId;
        ReviewerId = reviewerId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Notes = notes;
        ReviewedAt = reviewedAt;
    }

    internal static AppealReview Record(
        AppealId appealId,
        UserId reviewerId,
        AppealStatus fromStatus,
        AppealStatus toStatus,
        string? notes,
        DateTimeOffset reviewedAt)
    {
        ArgumentNullException.ThrowIfNull(appealId);
        ArgumentNullException.ThrowIfNull(reviewerId);

        var trimmed = string.IsNullOrWhiteSpace(notes)
            ? null
            : notes.Trim().Length <= MaxNotesLength ? notes.Trim() : notes.Trim()[..MaxNotesLength];

        return new AppealReview(
            AppealReviewId.New(), appealId, reviewerId, fromStatus, toStatus, trimmed, reviewedAt);
    }
}
