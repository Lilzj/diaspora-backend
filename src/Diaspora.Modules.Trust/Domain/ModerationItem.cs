using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>
/// Something waiting for a moderator to look at it.
/// <para>
/// Trust owns the <em>queue</em>, not the content. <see cref="ContentId"/> is a string and
/// <see cref="ContentType"/> an enum because the guide, comment or listing itself belongs to
/// whichever module publishes it — Trust holds the decision, the assignment and the audit
/// trail, and tells the owning module the outcome through an integration event.
/// </para>
/// <para>
/// That separation is what lets a Marketplace module get moderation without Trust learning
/// what a listing is.
/// </para>
/// </summary>
public sealed class ModerationItem : AggregateRoot<ModerationItemId>, IAuditableEntity
{
    public const int MaxContentIdLength = 128;
    public const int MaxTitleLength = 300;
    public const int MaxReasonLength = 1000;
    public const int MaxFlagLength = 64;
    public const int MaxFlags = 10;

    private readonly List<ModerationDecision> _decisions = [];
    private readonly List<string> _aiFlags = [];

    public ModerationContentType ContentType { get; private set; }

    public string ContentId { get; private set; } = default!;

    public string? Title { get; private set; }

    /// <summary>Who published the content. Not the reporter.</summary>
    public UserId AuthorId { get; private set; } = default!;

    public ModerationQueueStatus Status { get; private set; }

    public ModerationPriority Priority { get; private set; }

    // ── Automated pre-screen ─────────────────────────────────────────────

    /// <summary>Null until an automated review has run.</summary>
    public bool? AiReviewClean { get; private set; }

    /// <summary>0–1. How sure the automated review was.</summary>
    public decimal? AiConfidence { get; private set; }

    /// <summary>
    /// What the automated review objected to — "spam", "hate_speech".
    /// <para>
    /// Strings in the frontend's snake_case, kept as strings rather than an enum so a new
    /// classifier label does not need a Trust deployment to appear in the queue.
    /// </para>
    /// </summary>
    public IReadOnlyList<string> AiFlags => _aiFlags.AsReadOnly();

    public DateTimeOffset? AiReviewedAt { get; private set; }

    // ── Community reports ────────────────────────────────────────────────

    public int ReportCount { get; private set; }

    // ── Assignment and SLA ───────────────────────────────────────────────

    public UserId? AssignedModeratorId { get; private set; }

    public DateTimeOffset? AssignedAt { get; private set; }

    /// <summary>When the decision should have been made. Drives the overdue view.</summary>
    public DateTimeOffset? DueAt { get; private set; }

    public DateTimeOffset? ResolvedAt { get; private set; }

    public IReadOnlyList<ModerationDecision> Decisions => _decisions.AsReadOnly();

    public DateTimeOffset CreatedAt { get; private set; }
    public string? CreatedBy { get; private set; }
    public DateTimeOffset? ModifiedAt { get; private set; }
    public string? ModifiedBy { get; private set; }

    private ModerationItem() { }

    private ModerationItem(
        ModerationItemId id,
        ModerationContentType contentType,
        string contentId,
        UserId authorId,
        DateTimeOffset at) : base(id)
    {
        ContentType = contentType;
        ContentId = contentId;
        AuthorId = authorId;
        Status = ModerationQueueStatus.PendingReview;
        Priority = ModerationPriority.Normal;
        CreatedAt = at;
    }

    public static ModerationItem Enqueue(
        ModerationContentType contentType,
        string contentId,
        UserId authorId,
        DateTimeOffset at,
        string? title = null,
        ModerationPriority priority = ModerationPriority.Normal,
        TimeSpan? sla = null)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        var identifier = Truncate(
            Guard.Against.NullOrWhiteSpace(contentId, nameof(contentId)), MaxContentIdLength)!;

        return new ModerationItem(ModerationItemId.New(), contentType, identifier, authorId, at)
        {
            Title = Truncate(title, MaxTitleLength),
            Priority = priority,
            DueAt = sla is { } window ? at.Add(window) : null
        };
    }

    /// <summary>
    /// Records an automated pre-screen.
    /// <para>
    /// A flagged item is raised to <see cref="ModerationPriority.High"/> rather than acted on:
    /// the classifier's job is to order the queue, not to decide it. Only a moderator removes
    /// content.
    /// </para>
    /// </summary>
    public void RecordAiReview(
        bool clean,
        decimal confidence,
        IEnumerable<string> flags,
        DateTimeOffset at)
    {
        AiReviewClean = clean;
        AiConfidence = Math.Clamp(confidence, 0m, 1m);
        AiReviewedAt = at;

        _aiFlags.Clear();
        foreach (var flag in flags ?? [])
        {
            if (_aiFlags.Count >= MaxFlags)
            {
                break;
            }

            var normalized = Truncate(flag, MaxFlagLength);
            if (normalized is not null && !_aiFlags.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                _aiFlags.Add(normalized);
            }
        }

        if (!clean && Priority < ModerationPriority.High)
        {
            Priority = ModerationPriority.High;
        }

        if (Status == ModerationQueueStatus.AIReview)
        {
            Status = ModerationQueueStatus.PendingReview;
        }

        ModifiedAt = at;
    }

    /// <summary>
    /// Records a community report. Volume raises priority — a single report is noise, five
    /// is a signal.
    /// </summary>
    public void RecordReport(DateTimeOffset at)
    {
        ReportCount++;

        if (ReportCount >= 5 && Priority < ModerationPriority.Urgent)
        {
            Priority = ModerationPriority.Urgent;
        }
        else if (ReportCount >= 2 && Priority < ModerationPriority.High)
        {
            Priority = ModerationPriority.High;
        }

        ModifiedAt = at;
    }

    /// <summary>
    /// Takes the item onto a moderator's desk.
    /// <para>
    /// A moderator may not take their own content, for the same reason a reviewer may not
    /// approve their own verification.
    /// </para>
    /// </summary>
    public void Assign(UserId moderatorId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        if (moderatorId == AuthorId)
        {
            throw new DomainException(
                "A moderator cannot take their own content from the queue.",
                "trust.review.self-forbidden");
        }

        if (Status is ModerationQueueStatus.Approved or ModerationQueueStatus.Rejected
            or ModerationQueueStatus.Resolved)
        {
            throw new DomainException($"A queue item in status '{Status}' is already resolved.");
        }

        AssignedModeratorId = moderatorId;
        AssignedAt = at;
        ModifiedAt = at;
    }

    /// <summary>Puts an assigned item back in the queue.</summary>
    public void Release(DateTimeOffset at)
    {
        AssignedModeratorId = null;
        AssignedAt = null;
        ModifiedAt = at;
    }

    /// <summary>
    /// Records a moderator's decision.
    /// <para>
    /// The item must be assigned to the deciding moderator. Without that, two moderators can
    /// decide the same item differently in the same minute, and the queue stops being a
    /// workflow and becomes a race.
    /// </para>
    /// </summary>
    public ModerationDecision Decide(
        UserId moderatorId,
        ModerationActionType action,
        string? reason,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        if (moderatorId == AuthorId)
        {
            throw new DomainException(
                "A moderator cannot decide their own content.", "trust.review.self-forbidden");
        }

        if (AssignedModeratorId is null || AssignedModeratorId != moderatorId)
        {
            throw new DomainException(
                "The queue item must be assigned to you before you can decide it.",
                "trust.queue.not-assigned");
        }

        var previous = Status;
        var next = action switch
        {
            ModerationActionType.Approve or ModerationActionType.Restore
                or ModerationActionType.Unpin or ModerationActionType.Pin
                => ModerationQueueStatus.Approved,
            ModerationActionType.Reject or ModerationActionType.Hide
                or ModerationActionType.Archive or ModerationActionType.Suspend
                => ModerationQueueStatus.Rejected,
            ModerationActionType.Escalate => ModerationQueueStatus.Escalated,
            _ => Status
        };

        var decision = ModerationDecision.Record(
            Id, moderatorId, action, reason, previous, next, at);

        _decisions.Add(decision);

        Status = next;
        ModifiedAt = at;

        if (next is ModerationQueueStatus.Approved or ModerationQueueStatus.Rejected)
        {
            ResolvedAt = at;
        }

        RaiseDomainEvent(new ModerationDecisionRecordedDomainEvent(
            Id, moderatorId, ContentType, ContentId, action, at));

        return decision;
    }

    /// <summary>Reopens the item because the author appealed the decision.</summary>
    public void MarkAppealed(DateTimeOffset at)
    {
        if (Status is not (ModerationQueueStatus.Rejected or ModerationQueueStatus.Approved))
        {
            throw new DomainException(
                $"A queue item in status '{Status}' has no decision to appeal.");
        }

        Status = ModerationQueueStatus.Appealed;
        ResolvedAt = null;
        ModifiedAt = at;
    }

    /// <summary>Closes the item out after an appeal or escalation has run its course.</summary>
    public void Resolve(DateTimeOffset at)
    {
        Status = ModerationQueueStatus.Resolved;
        ResolvedAt = at;
        ModifiedAt = at;
    }

    public bool IsOpen => Status is ModerationQueueStatus.AIReview
        or ModerationQueueStatus.PendingReview
        or ModerationQueueStatus.Escalated
        or ModerationQueueStatus.Appealed;

    public bool IsOverdue(DateTimeOffset asOf) => IsOpen && DueAt is { } due && due < asOf;

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
