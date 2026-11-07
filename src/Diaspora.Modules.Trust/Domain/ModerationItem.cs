using Diaspora.Contracts.Moderation.V1;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Domain.Events;
using Diaspora.SharedKernel;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.Trust.Domain;

/// <summary>Why a case is waiting for a second member of staff.</summary>
public enum SecondReviewKind
{
    None = 0,

    /// <summary>One moderator hid it at once for an emergency reason. Checked within 24 hours.</summary>
    EmergencyHide = 1,

    /// <summary>Staff opened the case with no report. Checked within 7 days.</summary>
    StaffInitiated = 2,

    /// <summary>A moderator proposed removal. It takes effect only when a second confirms it.</summary>
    RemovalProposal = 3
}

/// <summary>
/// A moderation case: one piece of content, the reports about it, and every decision on it.
/// <para>
/// Trust owns the <em>case</em>, not the content. The content belongs to whichever module publishes
/// it; Trust asks it who owns the thing and tells it what was decided, through
/// <see cref="IModerationTarget"/>.
/// </para>
/// <para>
/// <b>Until 7 November 2026 nothing ever opened one.</b> Reports went to five module tables, and this
/// aggregate sat behind an empty queue with rules that would have hurt people once fed: a case could
/// be decided again and again, each decision recording another violation; penalties landed the moment
/// a moderator clicked; nobody had to check anybody. The owner's rules replace them:
/// </para>
/// <list type="bullet">
/// <item>A case is decided once. It changes only through a second review, the owner's request for a
/// review, or an appeal.</item>
/// <item>A removal needs a second member of staff. Until they confirm it, the content is hidden
/// pending review.</item>
/// <item>A hide for an emergency reason, and a case staff opened with no report, are checked by a
/// second member of staff.</item>
/// <item>No moderator acts on their own content, and nobody checks their own decision.</item>
/// </list>
/// </summary>
public sealed class ModerationItem : AggregateRoot<ModerationItemId>, IAuditableEntity
{
    public const int MaxContentIdLength = 128;
    public const int MaxTitleLength = 300;
    public const int MaxReasonLength = 1000;
    public const int MaxFlagLength = 64;
    public const int MaxFlags = 10;
    public const int MaxTargetTypeLength = 32;
    public const int MaxAreaLength = 32;

    private readonly List<ModerationDecision> _decisions = [];
    private readonly List<ModerationCaseReport> _reports = [];
    private readonly List<string> _aiFlags = [];

    public ModerationContentType ContentType { get; private set; }

    /// <summary>One of <see cref="ModerationContentTypes"/>: which module's content this is.</summary>
    public string? TargetType { get; private set; }

    /// <summary>One of <see cref="ModerationAreas"/>: where a strike for it would apply.</summary>
    public string? Area { get; private set; }

    public string ContentId { get; private set; } = default!;

    public string? Title { get; private set; }

    /// <summary>Who published the content. Not the reporter.</summary>
    public UserId AuthorId { get; private set; } = default!;

    public ModerationQueueStatus Status { get; private set; }

    public ModerationPriority Priority { get; private set; }

    // ── Why it is here ───────────────────────────────────────────────────

    /// <summary>The most serious reason any report gave, or the reason staff opened it for.</summary>
    public ModerationReasonCode? ReasonCode { get; private set; }

    /// <summary>Opened by staff with no report. A second member of staff checks it.</summary>
    public bool StaffInitiated { get; private set; }

    public UserId? OpenedById { get; private set; }

    public string? OpeningNote { get; private set; }

    /// <summary>
    /// Whether the case is in the queue. A single non-fraud report does not list it; a second, or one
    /// report of fraud, does.
    /// </summary>
    public bool IsListed { get; private set; }

    // ── Where the decision stands ────────────────────────────────────────

    /// <summary>What has been done to the content. Null until somebody decides.</summary>
    public ModerationOutcome? CurrentOutcome { get; private set; }

    public SecondReviewKind SecondReview { get; private set; }

    public DateTimeOffset? SecondReviewDueAt { get; private set; }

    /// <summary>The member of staff whose decision the second review checks. They may not check it.</summary>
    public UserId? FirstDeciderId { get; private set; }

    public ModerationReasonCode? ProposedReasonCode { get; private set; }

    public string? ProposedNote { get; private set; }

    /// <summary>Until when the owner may appeal a removal. Consequences wait for it.</summary>
    public DateTimeOffset? AppealDeadline { get; private set; }

    /// <summary>The owner fixed a hidden item and asked for it to be looked at again.</summary>
    public DateTimeOffset? ReviewRequestedAt { get; private set; }

    /// <summary>
    /// Hidden by the system after many serious reports, and no person has decided it yet. A
    /// moderator's "no action" on it puts the content back.
    /// </summary>
    public bool HiddenAutomatically { get; private set; }

    // ── Automated pre-screen ─────────────────────────────────────────────

    public bool? AiReviewClean { get; private set; }

    public decimal? AiConfidence { get; private set; }

    public IReadOnlyList<string> AiFlags => _aiFlags.AsReadOnly();

    public DateTimeOffset? AiReviewedAt { get; private set; }

    // ── Reports ──────────────────────────────────────────────────────────

    public int ReportCount { get; private set; }

    public IReadOnlyList<ModerationCaseReport> Reports => _reports.AsReadOnly();

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

    /// <summary>
    /// A case for content of the legacy kinds, queued straight away. Kept for the automated
    /// pre-screen; reports and staff open cases through <see cref="OpenForReports"/> and
    /// <see cref="OpenByStaff"/>.
    /// </summary>
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
            DueAt = sla is { } window ? at.Add(window) : null,
            IsListed = true
        };
    }

    /// <summary>
    /// The case for a piece of content that members have started to report. Not listed until the
    /// reports say it should be: see <see cref="AddReport"/>.
    /// </summary>
    public static ModerationItem OpenForReports(
        string targetType,
        string area,
        Guid contentId,
        UserId ownerId,
        string? title,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        return new ModerationItem(
            ModerationItemId.New(), LegacyTypeFor(targetType), contentId.ToString(), ownerId, at)
        {
            TargetType = Truncate(Guard.Against.NullOrWhiteSpace(targetType, nameof(targetType)), MaxTargetTypeLength),
            Area = Truncate(area, MaxAreaLength),
            Title = Truncate(title, MaxTitleLength)
        };
    }

    /// <summary>
    /// A case staff opened with no report, because staff do find scams first. Listed at once, with a
    /// reason and a note, and checked by a second member of staff within
    /// <see cref="ModerationPolicy.StaffInitiatedCheck"/>.
    /// </summary>
    public static ModerationItem OpenByStaff(
        string targetType,
        string area,
        Guid contentId,
        UserId ownerId,
        string? title,
        UserId openedBy,
        ModerationReasonCode reasonCode,
        string note,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(ownerId);
        ArgumentNullException.ThrowIfNull(openedBy);

        if (openedBy == ownerId)
        {
            throw new DomainException(
                "A member of staff cannot open a case on their own content.", "trust.review.self-forbidden");
        }

        var item = OpenForReports(targetType, area, contentId, ownerId, title, at);

        item.StaffInitiated = true;
        item.OpenedById = openedBy;
        item.OpeningNote = Truncate(RequireNote(note), MaxReasonLength);
        item.ReasonCode = reasonCode;
        item.IsListed = true;
        item.SecondReview = SecondReviewKind.StaffInitiated;
        item.SecondReviewDueAt = at.Add(ModerationPolicy.StaffInitiatedCheck);
        item.FirstDeciderId = openedBy;

        if (ModerationReasons.IsEmergency(reasonCode))
        {
            item.Priority = ModerationPriority.Urgent;
        }

        return item;
    }

    // ── Reports ──────────────────────────────────────────────────────────

    /// <summary>
    /// Adds a member's report. Returns false when the same member already reported it.
    /// <para>
    /// A fraud or scam report lists the case on its own; anything else needs a second report.
    /// Priority follows the count, and the case's reason becomes the most serious one given.
    /// </para>
    /// </summary>
    public bool AddReport(
        UserId reporterId,
        ModerationReasonCode reasonCode,
        string moduleReason,
        string? detail,
        string module,
        Guid moduleReportId,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(reporterId);

        if (_reports.Any(report => report.ReporterId == reporterId))
        {
            return false;
        }

        _reports.Add(ModerationCaseReport.File(
            Id, reporterId, reasonCode, moduleReason, detail, module, moduleReportId, at));

        RecordReport(at);

        if (ReasonCode is null || Severity(reasonCode) > Severity(ReasonCode.Value))
        {
            ReasonCode = reasonCode;
        }

        if (ModerationReasons.QueuesOnFirstReport(reasonCode)
            || _reports.Count >= ModerationPolicy.ReportsToQueue)
        {
            IsListed = true;
        }

        if (ModerationReasons.IsEmergency(reasonCode) && Priority < ModerationPriority.High)
        {
            Priority = ModerationPriority.High;
        }

        return true;
    }

    /// <summary>
    /// Whether enough serious reports have arrived to hide the content before anyone has looked:
    /// <see cref="ModerationPolicy.AutomaticHideReports"/> of them, at least one for a serious
    /// reason, and nobody has decided anything yet.
    /// </summary>
    public bool ShouldHideAutomatically() =>
        CurrentOutcome is null
        && _reports.Count >= ModerationPolicy.AutomaticHideReports
        && _reports.Any(report => ModerationReasons.IsSerious(report.ReasonCode));

    /// <summary>Records a community report. Volume raises priority.</summary>
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
    /// Records an automated pre-screen. A flagged item is raised in priority, never acted on: the
    /// classifier orders the queue, it does not decide it.
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

    // ── Assignment ───────────────────────────────────────────────────────

    /// <summary>
    /// Takes the case onto a moderator's desk. A moderator may not take their own content, for the
    /// same reason a reviewer may not approve their own verification.
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

        if (Status is ModerationQueueStatus.Resolved)
        {
            throw new DomainException($"A queue item in status '{Status}' is already resolved.");
        }

        AssignedModeratorId = moderatorId;
        AssignedAt = at;
        ModifiedAt = at;
    }

    /// <summary>Puts an assigned case back in the queue.</summary>
    public void Release(DateTimeOffset at)
    {
        AssignedModeratorId = null;
        AssignedAt = null;
        ModifiedAt = at;
    }

    // ── Decisions ────────────────────────────────────────────────────────

    /// <summary>Whether a moderator may make the case's decision now.</summary>
    public bool AwaitsDecision =>
        CurrentOutcome is null || HiddenAutomatically || ReviewRequestedAt is not null;

    /// <summary>
    /// A moderator's decision on the case. Returns what the content's module must do, or null when
    /// nothing changes on the content (a removal already hidden pending its confirmation).
    /// <para>
    /// Decided once: a case already decided is refused until the owner asks for a review. A removal
    /// is recorded as a proposal and the content hidden meanwhile; a second member of staff confirms
    /// it with <see cref="CompleteSecondReview"/>.
    /// </para>
    /// </summary>
    public (ModerationDecision Decision, ModerationOutcome? Apply) Decide(
        UserId moderatorId,
        ModerationOutcome outcome,
        ModerationReasonCode reasonCode,
        string note,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(moderatorId);

        var text = Truncate(RequireNote(note), MaxReasonLength)!;

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

        if (!AwaitsDecision)
        {
            throw new DomainException(
                "This case has been decided. It changes only through a second review, the owner's "
                + "request for a review, or an appeal.",
                "trust.case.already-decided");
        }

        if (SecondReview is SecondReviewKind.RemovalProposal)
        {
            throw new DomainException(
                "A removal on this case is waiting for a second member of staff.",
                "trust.case.awaiting-second-review");
        }

        var reopenedByOwner = ReviewRequestedAt is not null;
        var hiddenNow = CurrentOutcome is ModerationOutcome.HidePendingReview or ModerationOutcome.Remove;

        if (outcome is ModerationOutcome.Restore && !hiddenNow)
        {
            throw new DomainException(
                "There is nothing to restore: the content has not been taken down.",
                "trust.case.nothing-to-restore");
        }

        if (outcome is ModerationOutcome.NoAction && reopenedByOwner && !HiddenAutomatically)
        {
            throw new DomainException(
                "The owner asked for a review of something hidden. Restore it, keep it hidden, or "
                + "propose its removal.",
                "trust.case.outcome-invalid");
        }

        var previous = Status;
        ModerationOutcome? apply;
        ModerationOutcome? announce;

        switch (outcome)
        {
            case ModerationOutcome.NoAction:
                // Found to be fine. If the system had hidden it, it goes back up.
                apply = HiddenAutomatically ? ModerationOutcome.Restore : ModerationOutcome.NoAction;
                announce = apply;
                CurrentOutcome = apply;
                Close(at);
                break;

            case ModerationOutcome.HidePendingReview:
                apply = hiddenNow ? null : outcome;
                announce = outcome;
                CurrentOutcome = ModerationOutcome.HidePendingReview;
                Status = ModerationQueueStatus.Rejected;

                if (ModerationReasons.IsEmergency(reasonCode) && SecondReview is SecondReviewKind.None
                    && !hiddenNow)
                {
                    SecondReview = SecondReviewKind.EmergencyHide;
                    SecondReviewDueAt = at.Add(ModerationPolicy.EmergencySecondReview);
                    FirstDeciderId = moderatorId;
                    Priority = ModerationPriority.Urgent;
                }
                else if (SecondReview is SecondReviewKind.None)
                {
                    ResolvedAt = at;
                }

                break;

            case ModerationOutcome.Remove:
                // A proposal. Until a second member of staff confirms it, the content is hidden
                // pending review, and the owner is told that much if it was not hidden already.
                apply = hiddenNow ? null : ModerationOutcome.HidePendingReview;
                announce = apply;
                CurrentOutcome = ModerationOutcome.HidePendingReview;
                Status = ModerationQueueStatus.Escalated;
                SecondReview = SecondReviewKind.RemovalProposal;
                SecondReviewDueAt = at.Add(ModerationPolicy.StaffInitiatedCheck);
                FirstDeciderId = moderatorId;
                ProposedReasonCode = reasonCode;
                ProposedNote = text;
                break;

            case ModerationOutcome.Restore:
                apply = ModerationOutcome.Restore;
                announce = ModerationOutcome.Restore;
                CurrentOutcome = ModerationOutcome.Restore;
                AppealDeadline = null;
                Close(at);
                break;

            default:
                throw new DomainException($"'{outcome}' is not a decision.", "trust.case.outcome-invalid");
        }

        HiddenAutomatically = false;
        ReviewRequestedAt = null;
        ReasonCode ??= reasonCode;

        var decision = ModerationDecision.Record(
            Id,
            moderatorId,
            outcome == ModerationOutcome.Remove ? ModerationDecisionKind.Proposal : ModerationDecisionKind.Decision,
            outcome,
            reasonCode,
            text,
            previous,
            Status,
            at);

        _decisions.Add(decision);
        ModifiedAt = at;

        // Told every time a person decides, not only when the content changes: a moderator keeping
        // an automatic hide is a decision the owner hears about, with that moderator's note.
        if (announce is { } told)
        {
            RaiseActionTaken(decision, told, moderatorId, confirmedBy: null, at);
        }

        return (decision, apply);
    }

    /// <summary>
    /// A second member of staff checks the first one's decision: a proposed removal, an emergency
    /// hide, or a case staff opened with no report.
    /// <para>
    /// Confirming a removal makes it take effect and opens the owner's appeal window. Declining it
    /// puts the reviewer's own choice in its place: keep it hidden pending review, take no action, or
    /// restore it. Declining an emergency hide or a staff-opened case restores the content.
    /// </para>
    /// </summary>
    public (ModerationDecision Decision, ModerationOutcome? Apply) CompleteSecondReview(
        UserId reviewerId,
        bool confirm,
        ModerationOutcome? instead,
        string note,
        DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(reviewerId);

        var text = Truncate(RequireNote(note), MaxReasonLength)!;

        if (SecondReview is SecondReviewKind.None)
        {
            throw new DomainException(
                "Nothing on this case is waiting for a second member of staff.",
                "trust.case.no-second-review");
        }

        if (reviewerId == AuthorId)
        {
            throw new DomainException(
                "A member of staff cannot review a decision on their own content.",
                "trust.review.self-forbidden");
        }

        if (reviewerId == FirstDeciderId || reviewerId == OpenedById)
        {
            throw new DomainException(
                "The second review must be made by somebody other than the member of staff who acted.",
                "trust.case.same-reviewer");
        }

        var kind = SecondReview;
        var previous = Status;
        ModerationOutcome? apply = null;
        ModerationOutcome? announce = null;
        var code = ProposedReasonCode ?? ReasonCode ?? ModerationReasonCode.Other;
        ModerationOutcome recorded;

        if (kind is SecondReviewKind.RemovalProposal)
        {
            if (confirm)
            {
                apply = ModerationOutcome.Remove;
                CurrentOutcome = ModerationOutcome.Remove;
                AppealDeadline = at.Add(ModerationPolicy.AppealWindow);
                Status = ModerationQueueStatus.Rejected;
                ResolvedAt = at;
                recorded = ModerationOutcome.Remove;
            }
            else
            {
                var alternative = instead ?? ModerationOutcome.HidePendingReview;

                if (alternative is ModerationOutcome.Remove)
                {
                    throw new DomainException(
                        "Declining a removal cannot be a removal.", "trust.case.outcome-invalid");
                }

                recorded = alternative;

                switch (alternative)
                {
                    case ModerationOutcome.HidePendingReview:
                        Status = ModerationQueueStatus.Rejected;
                        ResolvedAt = at;
                        announce = ModerationOutcome.HidePendingReview;
                        break;

                    case ModerationOutcome.NoAction:
                    case ModerationOutcome.Restore:
                        apply = ModerationOutcome.Restore;
                        CurrentOutcome = ModerationOutcome.Restore;
                        Close(at);
                        break;
                }
            }
        }
        else
        {
            recorded = confirm ? CurrentOutcome ?? ModerationOutcome.NoAction : ModerationOutcome.Restore;

            if (confirm)
            {
                if (CurrentOutcome is null)
                {
                    // A staff-opened case confirmed before anyone decided it: it stays in the queue.
                    Status = ModerationQueueStatus.PendingReview;
                }
                else
                {
                    ResolvedAt = at;
                }
            }
            else if (CurrentOutcome is ModerationOutcome.HidePendingReview or ModerationOutcome.Remove)
            {
                apply = ModerationOutcome.Restore;
                CurrentOutcome = ModerationOutcome.Restore;
                Close(at);
            }
            else
            {
                CurrentOutcome ??= ModerationOutcome.NoAction;
                Close(at);
            }
        }

        SecondReview = SecondReviewKind.None;
        SecondReviewDueAt = null;

        var decision = ModerationDecision.Record(
            Id,
            reviewerId,
            confirm ? ModerationDecisionKind.Confirmation : ModerationDecisionKind.Overruled,
            recorded,
            code,
            text,
            previous,
            Status,
            at);

        _decisions.Add(decision);
        ModifiedAt = at;

        if ((apply ?? announce) is { } effective)
        {
            RaiseActionTaken(
                decision,
                effective,
                effective is ModerationOutcome.Remove ? FirstDeciderId : reviewerId,
                confirmedBy: effective is ModerationOutcome.Remove ? reviewerId : null,
                at,
                noteOverride: effective is ModerationOutcome.Remove ? ProposedNote : null);
        }

        return (decision, apply);
    }

    /// <summary>
    /// The system hides the content after many serious reports, before anyone has looked. The owner is
    /// told at once, and the case goes to the top of the queue for a person to decide.
    /// </summary>
    public ModerationDecision HideAutomatically(DateTimeOffset at)
    {
        if (!ShouldHideAutomatically())
        {
            throw new DomainException("This case does not meet the automatic hide.", "trust.case.outcome-invalid");
        }

        var code = ReasonCode ?? ModerationReasonCode.Other;
        var note = AutomaticHideNote(_reports.Count);
        var previous = Status;

        CurrentOutcome = ModerationOutcome.HidePendingReview;
        Priority = ModerationPriority.Urgent;
        IsListed = true;
        // Still waiting for a person: the system hid it, nobody has decided it.
        HiddenAutomatically = true;

        var decision = ModerationDecision.Record(
            Id, moderatorId: null, ModerationDecisionKind.System, ModerationOutcome.HidePendingReview,
            code, note, previous, Status, at);

        _decisions.Add(decision);
        ModifiedAt = at;

        RaiseActionTaken(decision, ModerationOutcome.HidePendingReview, decidedBy: null, confirmedBy: null, at);

        return decision;
    }

    /// <summary>What the owner reads when the system hid their content.</summary>
    public static string AutomaticHideNote(int reports) =>
        $"Hidden automatically after {reports} reports, pending a review by Diaspora staff.";

    /// <summary>
    /// The owner fixed what was hidden and asks for it to be looked at again. The case goes back in
    /// the queue for a moderator to restore it or not.
    /// </summary>
    public void RequestReview(UserId ownerId, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(ownerId);

        if (ownerId != AuthorId)
        {
            throw new DomainException("Only the owner can ask for a review.", "trust.case.not-owner");
        }

        if (CurrentOutcome is not ModerationOutcome.HidePendingReview
            || SecondReview is SecondReviewKind.RemovalProposal)
        {
            throw new DomainException(
                "Only something hidden pending review can be put back in front of a moderator.",
                "trust.case.review-not-open");
        }

        ReviewRequestedAt = at;
        Status = ModerationQueueStatus.PendingReview;
        IsListed = true;
        AssignedModeratorId = null;
        AssignedAt = null;
        ResolvedAt = null;
        ModifiedAt = at;
    }

    /// <summary>Reopens the item because the author appealed the decision.</summary>
    public void MarkAppealed(DateTimeOffset at)
    {
        if (Status is not (ModerationQueueStatus.Rejected or ModerationQueueStatus.Approved
            or ModerationQueueStatus.Resolved))
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

    /// <summary>Open for reports: not closed with no action, and not restored.</summary>
    public bool AcceptsReports => Status is not ModerationQueueStatus.Resolved;

    public bool IsOverdue(DateTimeOffset asOf) =>
        (IsOpen && DueAt is { } due && due < asOf)
        || (SecondReviewDueAt is { } second && second < asOf);

    private void Close(DateTimeOffset at)
    {
        Status = ModerationQueueStatus.Resolved;
        ResolvedAt = at;
        SecondReview = SecondReviewKind.None;
        SecondReviewDueAt = null;
        AssignedModeratorId = null;
        AssignedAt = null;
    }

    private void RaiseActionTaken(
        ModerationDecision decision,
        ModerationOutcome outcome,
        UserId? decidedBy,
        UserId? confirmedBy,
        DateTimeOffset at,
        string? noteOverride = null)
    {
        RaiseDomainEvent(new ModerationActionTakenDomainEvent(
            Id,
            decision.Id,
            TargetType ?? ContentType.ToString(),
            ContentId,
            Title,
            Area ?? string.Empty,
            AuthorId,
            outcome,
            decision.ReasonCode ?? ReasonCode ?? ModerationReasonCode.Other,
            noteOverride ?? decision.Reason ?? string.Empty,
            decidedBy,
            confirmedBy,
            outcome is ModerationOutcome.Remove ? AppealDeadline : null,
            [.. _reports.Select(report => report.ReporterId.Value).Distinct()],
            [.. _reports.Select(report => report.ModuleReportId).Distinct()],
            at));
    }

    private static string RequireNote(string? note)
    {
        var trimmed = note?.Trim() ?? string.Empty;

        if (trimmed.Length < ModerationPolicy.MinimumNoteLength)
        {
            throw new DomainException(
                $"A decision needs a note of at least {ModerationPolicy.MinimumNoteLength} characters: "
                + "it is what the owner reads.",
                "trust.case.note-required");
        }

        return trimmed;
    }

    /// <summary>How serious a reason is, for choosing the case's leading reason.</summary>
    private static int Severity(ModerationReasonCode code) => code switch
    {
        ModerationReasonCode.ChildSafety => 100,
        ModerationReasonCode.ViolenceOrThreats => 90,
        ModerationReasonCode.AsksForFees => 80,
        ModerationReasonCode.FraudOrScam => 75,
        ModerationReasonCode.Impersonation => 70,
        ModerationReasonCode.FalseOrMisleading => 60,
        ModerationReasonCode.Discriminatory => 55,
        ModerationReasonCode.OffensiveOrHarassment => 50,
        ModerationReasonCode.ProhibitedItem => 45,
        ModerationReasonCode.PersonalData => 40,
        ModerationReasonCode.Copyright => 30,
        ModerationReasonCode.Spam => 20,
        ModerationReasonCode.Duplicate => 10,
        _ => 0
    };

    /// <summary>The nearest of the legacy content kinds, for the column that predates targets.</summary>
    private static ModerationContentType LegacyTypeFor(string targetType) => targetType switch
    {
        ModerationContentTypes.Guide => ModerationContentType.Guide,
        ModerationContentTypes.CommunityPost => ModerationContentType.CommunityUpdate,
        ModerationContentTypes.CommunityComment => ModerationContentType.Comment,
        ModerationContentTypes.Organization => ModerationContentType.Organization,
        _ => ModerationContentType.Listing
    };

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
