using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// A member's challenge to a trust decision that went against them.
/// <para>
/// Its own aggregate rather than a field on the thing being appealed, for two reasons. A
/// member may appeal a suspension or a content removal that Trust does not own the record of,
/// so <see cref="TargetId"/> is a string; and an appeal outlives its subject — the appeal
/// trail must survive the verification being reopened and decided again.
/// </para>
/// <para>
/// The invariant that gives an appeals process its legitimacy is enforced in
/// <see cref="Decide"/>: the reviewer of an appeal may be neither the member nor the person
/// whose decision is under appeal.
/// </para>
/// </summary>
public sealed class Appeal : AggregateRoot<AppealId>, IAuditableEntity
{
    public const int MaxReasonLength = 2000;
    public const int MaxExplanationLength = 2000;
    public const int MaxTargetIdLength = 128;
    public const int MaxEvidenceItems = 10;

    /// <summary>How long after a decision a member may still appeal it.</summary>
    public static readonly TimeSpan AppealWindow = TimeSpan.FromDays(90);

    private readonly List<AppealReview> _reviews = [];
    private readonly List<string> _evidenceKeys = [];

    public UserId UserId { get; private set; } = default!;

    public AppealType Type { get; private set; }

    /// <summary>
    /// What is being appealed. A verification id, a content id, a suspension reference —
    /// a string because the appealed thing frequently belongs to another module.
    /// </summary>
    public string TargetId { get; private set; } = default!;

    public AppealStatus Status { get; private set; }

    /// <summary>The member's own argument.</summary>
    public string Reason { get; private set; } = default!;

    /// <summary>The platform's reply. Rendered to the member as reviewer comments.</summary>
    public string? Explanation { get; private set; }

    /// <summary>Whether the member is asking for a correction rather than a reversal.</summary>
    public bool IsCorrectionRequest { get; private set; }

    /// <summary>Storage keys for supporting evidence attached to the appeal.</summary>
    public IReadOnlyList<string> EvidenceKeys => _evidenceKeys.AsReadOnly();

    public IReadOnlyList<AppealReview> Reviews => _reviews.AsReadOnly();

    /// <summary>Who made the decision under appeal. Barred from reviewing it.</summary>
    public UserId? OriginalDeciderId { get; private set; }

    public UserId? AssignedReviewerId { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    /// <summary>When review should have finished. Drives the overdue appeals queue.</summary>
    public DateTimeOffset? ReviewDueAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private Appeal() { }

    private Appeal(
        AppealId id,
        UserId userId,
        AppealType type,
        string targetId,
        string reason,
        DateTimeOffset submittedAt) : base(id)
    {
        UserId = userId;
        Type = type;
        TargetId = targetId;
        Reason = reason;
        Status = AppealStatus.Pending;
        SubmittedAt = submittedAt;
    }

    /// <summary>
    /// Files an appeal.
    /// <para>
    /// The <paramref name="decidedAt"/> of the original decision is taken so the appeal
    /// window can be enforced here rather than in a handler — a rule about what a member may
    /// still do belongs with the thing it constrains.
    /// </para>
    /// </summary>
    public static Appeal File(
        UserId userId,
        AppealType type,
        string targetId,
        string reason,
        DateTimeOffset submittedAt,
        DateTimeOffset? decidedAt = null,
        TimeSpan? reviewSla = null)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (decidedAt is { } decided && submittedAt - decided > AppealWindow)
        {
            throw new DomainException(
                $"The {AppealWindow.TotalDays:0}-day window to appeal this decision has closed.",
                "trust.appeal.window-closed");
        }

        var target = Truncate(
            Guard.Against.NullOrWhiteSpace(targetId, nameof(targetId)), MaxTargetIdLength)!;

        var justification = Truncate(
            Guard.Against.NullOrWhiteSpace(reason, nameof(reason)), MaxReasonLength)!;

        var appeal = new Appeal(AppealId.New(), userId, type, target, justification, submittedAt)
        {
            ReviewDueAt = reviewSla is { } sla ? submittedAt.Add(sla) : null,

            // What the member sees while they wait. The appeals screen renders this as
            // "Moderator review comments", so it must never be empty.
            Explanation = "Our governance team is reviewing your appeal request."
        };

        appeal.RaiseDomainEvent(new AppealSubmittedDomainEvent(
            appeal.Id, userId, type, target, submittedAt));

        return appeal;
    }

    public Appeal WithOriginalDecider(UserId? deciderId)
    {
        OriginalDeciderId = deciderId;
        return this;
    }

    public Appeal AsCorrectionRequest(bool isCorrection)
    {
        IsCorrectionRequest = isCorrection;
        return this;
    }

    /// <summary>Attaches supporting evidence. Only while the appeal is still open.</summary>
    public void AttachEvidence(string storageKey)
    {
        if (Status is AppealStatus.Approved or AppealStatus.Rejected or AppealStatus.Withdrawn)
        {
            throw new DomainException(
                $"Evidence cannot be added to an appeal in status '{Status}'.");
        }

        if (_evidenceKeys.Count >= MaxEvidenceItems)
        {
            throw new DomainException(
                $"An appeal may carry at most {MaxEvidenceItems} evidence items.");
        }

        var key = Guard.Against.NullOrWhiteSpace(storageKey, nameof(storageKey)).Trim();
        if (!_evidenceKeys.Contains(key, StringComparer.Ordinal))
        {
            _evidenceKeys.Add(key);
        }
    }

    /// <summary>Takes the appeal onto a reviewer's desk.</summary>
    public void Assign(UserId reviewerId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);
        GuardReviewerEligible(reviewerId);

        if (Status is not (AppealStatus.Pending or AppealStatus.UnderReview or AppealStatus.Escalated))
        {
            throw new DomainException($"An appeal in status '{Status}' cannot be assigned.");
        }

        AssignedReviewerId = reviewerId;

        if (Status == AppealStatus.Pending)
        {
            Status = AppealStatus.UnderReview;
            ModifiedAt = at;
        }
    }

    /// <summary>Escalates the appeal beyond routine review.</summary>
    public void Escalate(UserId reviewerId, string? notes, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);

        if (Status is AppealStatus.Approved or AppealStatus.Rejected or AppealStatus.Withdrawn)
        {
            throw new DomainException($"An appeal in status '{Status}' is already resolved.");
        }

        _reviews.Add(AppealReview.Record(
            Id, reviewerId, Status, AppealStatus.Escalated, notes, at));

        Status = AppealStatus.Escalated;
        ModifiedAt = at;
    }

    /// <summary>
    /// Decides the appeal.
    /// <para>
    /// Both eligibility rules are enforced here, not in the handler: a member may not review
    /// their own appeal, and neither may the person whose decision is being appealed. An
    /// appeals process that permits either is not an appeals process.
    /// </para>
    /// </summary>
    public void Decide(
        UserId reviewerId,
        bool upheld,
        string explanation,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);
        GuardReviewerEligible(reviewerId);

        if (Status is AppealStatus.Approved or AppealStatus.Rejected or AppealStatus.Withdrawn)
        {
            throw new DomainException($"An appeal in status '{Status}' is already resolved.");
        }

        var target = upheld ? AppealStatus.Approved : AppealStatus.Rejected;

        _reviews.Add(AppealReview.Record(Id, reviewerId, Status, target, explanation, at));

        Status = target;
        Explanation = Truncate(
            Guard.Against.NullOrWhiteSpace(explanation, nameof(explanation)), MaxExplanationLength);
        ResolvedAt = at;
        ModifiedAt = at;

        RaiseDomainEvent(upheld
            ? new AppealApprovedDomainEvent(Id, UserId, Type, TargetId, Explanation, at)
            : new AppealRejectedDomainEvent(Id, UserId, Type, TargetId, Explanation, at));
    }

    /// <summary>The member withdraws their own appeal.</summary>
    public void Withdraw(DateTimeOffset at)
    {
        if (Status is AppealStatus.Approved or AppealStatus.Rejected)
        {
            throw new DomainException($"An appeal in status '{Status}' is already resolved.");
        }

        Status = AppealStatus.Withdrawn;
        ResolvedAt = at;
        ModifiedAt = at;
        Explanation = "Withdrawn by the member.";
    }

    /// <summary>Whether the appeal is still awaiting an outcome.</summary>
    public bool IsOpen =>
        Status is AppealStatus.Pending or AppealStatus.UnderReview or AppealStatus.Escalated;

    public bool IsOverdue(DateTimeOffset asOf) => IsOpen && ReviewDueAt is { } due && due < asOf;

    private void GuardReviewerEligible(UserId reviewerId)
    {
        if (reviewerId == UserId)
        {
            throw new DomainException(
                "A member cannot review their own appeal.", "trust.review.self-forbidden");
        }

        if (OriginalDeciderId is not null && reviewerId == OriginalDeciderId)
        {
            throw new DomainException(
                "The reviewer whose decision is under appeal cannot decide the appeal.",
                "trust.review.self-forbidden");
        }
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

    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }
}
