using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Applications;

/// <summary>
/// One candidate's approach to one job. The durable record of a hiring conversation.
/// <para>
/// <b>The history is what makes this an application rather than a form submission.</b> Every
/// transition, every note, every interview and every offer answer is appended to
/// <see cref="History"/> and never edited — so "when was I shortlisted", "how long did they sit
/// on it" and "what were we actually told" all have answers months later, to both parties.
/// That record is the platform's contribution to a process that is otherwise conducted entirely
/// in somebody's inbox.
/// </para>
/// <para>
/// <b>The asymmetry is deliberate.</b> An employer walks an application forward one legal step
/// at a time; a candidate may withdraw from anywhere before an ending. Anything else makes the
/// platform a trap for the party with the least power in the transaction.
/// </para>
/// <para>
/// <b>What it owns:</b> its answers, its attachments and its history — all frozen or
/// append-only after submission, because an employer must be looking at what they were sent.
/// <b>What it does not:</b> interviews and offers are separate aggregates keyed by application,
/// because both have their own lifecycles and an application should not have to be loaded to
/// reschedule a call.
/// </para>
/// <para>
/// <b>The posting is not mutated from here.</b> Accepting an offer fills a vacancy, and
/// submitting nudges the posting's status — both happen in a handler that loads the posting
/// aggregate properly, because reaching across an aggregate boundary to mutate is how two
/// consistency boundaries quietly become one.
/// </para>
/// </summary>
public sealed class JobApplication
    : AggregateRoot<JobApplicationId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private readonly List<ApplicationAnswer> _answers = [];
    private readonly List<ApplicationAttachment> _attachments = [];
    private readonly List<ApplicationHistoryEntry> _history = [];

    private JobApplication(
        JobApplicationId id,
        JobPostingId postingId,
        EmployerProfileId employerId,
        CandidateProfileId candidateId,
        UserId applicantUserId,
        ResumeId? resumeId,
        string? coverLetter,
        DateTimeOffset submittedAt)
        : base(id)
    {
        PostingId = postingId;
        EmployerId = employerId;
        CandidateId = candidateId;
        ApplicantUserId = applicantUserId;
        ResumeId = resumeId;
        CoverLetter = coverLetter;
        Status = ApplicationStatus.Submitted;
        SubmittedAt = submittedAt;
        LastActivityAt = submittedAt;
        CreatedAt = submittedAt;
    }

    private JobApplication() { }

    // ── Parties ─────────────────────────────────────────────────────────────────────────────

    public JobPostingId PostingId { get; private set; } = default!;

    /// <summary>
    /// Denormalised from the posting. An employer's applicant list is the single hottest query
    /// in the module, and joining every application to its posting to answer "whose is this"
    /// would make it a scan.
    /// </summary>
    public EmployerProfileId EmployerId { get; private set; } = default!;

    public CandidateProfileId CandidateId { get; private set; } = default!;

    /// <summary>The member. Denormalised so an ownership check needs no join.</summary>
    public UserId ApplicantUserId { get; private set; } = default!;

    /// <summary>The platform's ownership seam. The application is the candidate's.</summary>
    public Guid OwnerId => ApplicantUserId.Value;

    // ── Content, frozen at submission ───────────────────────────────────────────────────────

    /// <summary>The exact résumé version sent. Immutable, so the employer sees what they got.</summary>
    public ResumeId? ResumeId { get; private set; }

    public string? CoverLetter { get; private set; }

    // ── Workflow ────────────────────────────────────────────────────────────────────────────

    public ApplicationStatus Status { get; private set; }

    /// <summary>The employer's own pipeline stage. Their vocabulary, not the platform's.</summary>
    public string? StageKey { get; private set; }

    public DateTimeOffset SubmittedAt { get; private set; }

    /// <summary>When anything last happened. Drives the "no response" sweep and the sort.</summary>
    public DateTimeOffset LastActivityAt { get; private set; }

    /// <summary>When a human at the employer first opened it. Null while it sits unread.</summary>
    public DateTimeOffset? FirstViewedAt { get; private set; }

    /// <summary>When the employer first moved it. The response-time measurement's endpoint.</summary>
    public DateTimeOffset? FirstRespondedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public DateTimeOffset? WithdrawnAt { get; private set; }

    /// <summary>
    /// Why the application ended, when the employer supplied something the candidate may see.
    /// A rejection with a reason is rare and worth far more than one without.
    /// </summary>
    public string? DecisionReason { get; private set; }

    /// <summary>The optimistic concurrency token.</summary>
    public int Revision { get; private set; }

    // ── Owned collections ───────────────────────────────────────────────────────────────────

    public IReadOnlyList<ApplicationAnswer> Answers =>
        _answers.OrderBy(answer => answer.Order).ToList().AsReadOnly();

    public IReadOnlyList<ApplicationAttachment> Attachments =>
        _attachments.OrderBy(attachment => attachment.Order).ToList().AsReadOnly();

    /// <summary>The complete record, oldest first. Append-only.</summary>
    public IReadOnlyList<ApplicationHistoryEntry> History =>
        _history.OrderBy(entry => entry.OccurredAt).ToList().AsReadOnly();

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether the employer is still considering this candidate.</summary>
    public bool IsActive => !IsDeleted && ApplicationStatusTransitions.IsActive(Status);

    /// <summary>Whether the candidate can still walk away.</summary>
    public bool CanWithdraw => IsActive;

    /// <summary>Whether a human at the employer has opened it.</summary>
    public bool HasBeenViewed => FirstViewedAt is not null;

    /// <summary>
    /// How long the candidate waited for the employer's first move. Null while they are still
    /// waiting — which is not zero, and must never render as "responded immediately".
    /// </summary>
    public TimeSpan? TimeToFirstResponse =>
        FirstRespondedAt is { } responded ? responded - SubmittedAt : null;

    /// <summary>The candidate's own timeline: the entries they are allowed to see.</summary>
    public IReadOnlyList<ApplicationHistoryEntry> CandidateVisibleHistory =>
        _history.Where(entry => entry.IsVisibleToCandidate)
            .OrderBy(entry => entry.OccurredAt)
            .ToList()
            .AsReadOnly();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Submission
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Submits an application.
    /// <para>
    /// There is no draft state, deliberately. A half-finished application is a thing the client
    /// holds, not a row the employer might trip over — and an "applied" count that included
    /// abandoned drafts would mislead everyone reading it.
    /// </para>
    /// <para>
    /// Whether the posting is open, whether the candidate has already applied, and whether a
    /// required résumé is present are all checked by the handler, which can see the posting and
    /// the other applications. What the aggregate guarantees is that whatever is recorded here
    /// is well-formed and complete.
    /// </para>
    /// </summary>
    public static Result<JobApplication> Submit(
        JobApplicationId id,
        JobPostingId postingId,
        EmployerProfileId employerId,
        CandidateProfileId candidateId,
        UserId applicantUserId,
        ResumeId? resumeId,
        string? coverLetter,
        IReadOnlyList<ApplicationAnswer> answers,
        IReadOnlyList<ApplicationAttachment> attachments,
        DateTimeOffset now,
        string? categoryKey = null,
        string? countryCode = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(postingId);
        ArgumentNullException.ThrowIfNull(employerId);
        ArgumentNullException.ThrowIfNull(candidateId);
        ArgumentNullException.ThrowIfNull(applicantUserId);
        ArgumentNullException.ThrowIfNull(answers);
        ArgumentNullException.ThrowIfNull(attachments);

        var letter = string.IsNullOrWhiteSpace(coverLetter) ? null : coverLetter.Trim();

        if (letter is { Length: > JobLimits.MaxCoverLetter })
        {
            return Result.Failure<JobApplication>(JobErrors.CoverLetterTooLong);
        }

        if (answers.Count > JobLimits.MaxAnswers)
        {
            return Result.Failure<JobApplication>(JobErrors.AnswerLimit);
        }

        if (attachments.Count > JobLimits.MaxAttachments)
        {
            return Result.Failure<JobApplication>(JobErrors.AttachmentLimit);
        }

        var application = new JobApplication(
            id, postingId, employerId, candidateId, applicantUserId, resumeId, letter, now);

        application._answers.AddRange(answers);
        application._attachments.AddRange(attachments);

        application.Append(
            ApplicationEventKind.Submitted,
            fromStatus: null,
            toStatus: ApplicationStatus.Submitted,
            actorId: applicantUserId,
            note: null,
            isVisibleToCandidate: true,
            now);

        application.RaiseDomainEvent(new JobApplicationSubmittedDomainEvent
        {
            ApplicationId = id,
            PostingId = postingId,
            EmployerId = employerId,
            CandidateId = candidateId,
            ApplicantUserId = applicantUserId,
            SubmittedAt = now,

            // Attributes of the posting rather than of the applicant — see the domain event.
            // Defaulted so a caller that does not hold the posting still compiles; the one
            // caller that matters does hold it.
            CategoryKey = string.IsNullOrWhiteSpace(categoryKey) ? null : categoryKey.Trim(),
            CountryCode = string.IsNullOrWhiteSpace(countryCode) ? null : countryCode.Trim()
        });

        return Result.Success(application);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrency
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rejects a save based on a revision the caller no longer holds.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(JobErrors.RevisionConflict)
            : Result.Success();

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Workflow
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Moves the application through the hiring workflow.
    /// <para>
    /// The single mutator every employer-side decision goes through, so
    /// <see cref="ApplicationStatusTransitions"/> is consulted in one place. Withdrawal has its
    /// own method because it is the candidate's, carries different consequences, and must not be
    /// reachable from an employer-facing command by passing an enum value.
    /// </para>
    /// </summary>
    public Result MoveTo(
        ApplicationStatus target,
        DateTimeOffset now,
        UserId? changedBy,
        string? reason = null,
        bool reasonVisibleToCandidate = true)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.ApplicationNotFound);
        }

        if (target == ApplicationStatus.Withdrawn)
        {
            // The candidate's exit is not reachable through the employer's mutator.
            return Result.Failure(JobErrors.ApplicationTransitionInvalid);
        }

        if (ApplicationStatusTransitions.IsTerminal(Status))
        {
            return Result.Failure(JobErrors.ApplicationAlreadyDecided);
        }

        if (!ApplicationStatusTransitions.CanTransition(Status, target))
        {
            return Result.Failure(JobErrors.ApplicationTransitionInvalid);
        }

        var previous = Status;
        var isFirstResponse = FirstRespondedAt is null;
        var trimmed = Clip(reason, JobLimits.MaxApplicationNote);

        Status = target;
        LastActivityAt = now;
        FirstRespondedAt ??= now;

        if (ApplicationStatusTransitions.IsTerminal(target))
        {
            DecidedAt = now;
            DecisionReason = reasonVisibleToCandidate ? trimmed : null;
        }

        Touch();

        Append(
            ApplicationEventKind.StatusChanged,
            previous,
            target,
            changedBy,
            trimmed,
            reasonVisibleToCandidate,
            now);

        RaiseDomainEvent(new JobApplicationStatusChangedDomainEvent
        {
            ApplicationId = Id,
            PostingId = PostingId,
            EmployerId = EmployerId,
            CandidateId = CandidateId,
            PreviousStatus = previous,
            CurrentStatus = target,
            ChangedBy = changedBy,
            Reason = reasonVisibleToCandidate ? trimmed : null,
            IsFirstEmployerResponse = isFirstResponse,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// The candidate withdraws.
    /// <para>
    /// Available from every non-terminal state, including after an offer. The reason is always
    /// optional: requiring one to leave a hiring process would be a small cruelty, and an
    /// unwilling answer is worthless data anyway.
    /// </para>
    /// </summary>
    public Result Withdraw(DateTimeOffset now, UserId withdrawnBy, string? reason = null)
    {
        ArgumentNullException.ThrowIfNull(withdrawnBy);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.ApplicationNotFound);
        }

        if (Status == ApplicationStatus.Withdrawn)
        {
            return Result.Failure(JobErrors.ApplicationAlreadyWithdrawn);
        }

        if (ApplicationStatusTransitions.IsTerminal(Status))
        {
            return Result.Failure(JobErrors.ApplicationNotWithdrawable);
        }

        var previous = Status;
        var trimmed = Clip(reason, JobLimits.MaxApplicationNote);

        Status = ApplicationStatus.Withdrawn;
        WithdrawnAt = now;
        DecidedAt = now;
        LastActivityAt = now;
        DecisionReason = trimmed;
        Touch();

        Append(
            ApplicationEventKind.Withdrawn,
            previous,
            ApplicationStatus.Withdrawn,
            withdrawnBy,
            trimmed,
            isVisibleToCandidate: true,
            now);

        RaiseDomainEvent(new JobApplicationWithdrawnDomainEvent
        {
            ApplicationId = Id,
            PostingId = PostingId,
            EmployerId = EmployerId,
            CandidateId = CandidateId,
            PreviousStatus = previous,
            WithdrawnBy = withdrawnBy,
            Reason = trimmed,
            WithdrawnAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Moves the application to a different stage of the employer's own pipeline.
    /// <para>
    /// Independent of status: an employer may run three interview stages, and an application
    /// moving between them stays <c>InterviewCompleted</c> throughout. The stage key's existence
    /// on the pipeline is the handler's check — the aggregate cannot see the pipeline.
    /// </para>
    /// </summary>
    public Result MoveToStage(string? stageKey, DateTimeOffset now, UserId? changedBy)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.ApplicationNotFound);
        }

        if (ApplicationStatusTransitions.IsTerminal(Status))
        {
            return Result.Failure(JobErrors.ApplicationAlreadyDecided);
        }

        var key = NormalizeStageKey(stageKey);

        if (key is null)
        {
            return Result.Failure(JobErrors.StageKeyInvalid);
        }

        if (string.Equals(StageKey, key, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        var previous = StageKey;
        StageKey = key;
        LastActivityAt = now;
        Touch();

        Append(
            ApplicationEventKind.StageChanged,
            fromStatus: null,
            toStatus: null,
            changedBy,
            note: key,
            isVisibleToCandidate: false,
            now);

        RaiseDomainEvent(new JobApplicationStageChangedDomainEvent
        {
            ApplicationId = Id,
            PostingId = PostingId,
            PreviousStageKey = previous,
            CurrentStageKey = key,
            ChangedBy = changedBy,
            ChangedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Records that someone at the employer opened the application.
    /// <para>
    /// Idempotent — only the first view is a fact worth having, and a recruiter refreshing a
    /// list must not produce a thousand events. Deliberately no revision bump: reading is not
    /// editing, and it must never make a colleague's in-flight decision fail.
    /// </para>
    /// </summary>
    public Result RecordViewed(DateTimeOffset now, UserId viewedBy)
    {
        ArgumentNullException.ThrowIfNull(viewedBy);

        if (IsDeleted || FirstViewedAt is not null)
        {
            return Result.Success();
        }

        FirstViewedAt = now;

        Append(
            ApplicationEventKind.Viewed,
            fromStatus: null,
            toStatus: null,
            viewedBy,
            note: null,
            isVisibleToCandidate: false,
            now);

        RaiseDomainEvent(new JobApplicationViewedDomainEvent
        {
            ApplicationId = Id,
            PostingId = PostingId,
            EmployerId = EmployerId,
            ViewedBy = viewedBy,
            TimeToFirstView = now - SubmittedAt,
            ViewedAt = now
        });

        return Result.Success();
    }

    /// <summary>
    /// Appends an internal note. Never shown to the candidate, and never removable.
    /// </summary>
    public Result AddNote(
        Guid entryId, UserId authorId, string? note, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(authorId);

        if (IsDeleted)
        {
            return Result.Failure(JobErrors.ApplicationNotFound);
        }

        var trimmed = Clip(note, JobLimits.MaxApplicationNote);

        if (trimmed is null)
        {
            return Result.Failure(JobErrors.AnswerInvalid);
        }

        if (_history.Count >= JobLimits.MaxHistoryEntries)
        {
            return Result.Failure(JobErrors.AnswerLimit);
        }

        LastActivityAt = now;

        Append(
            ApplicationEventKind.NoteAdded,
            fromStatus: null,
            toStatus: null,
            authorId,
            trimmed,
            isVisibleToCandidate: false,
            now,
            entryId);

        return Result.Success();
    }

    /// <summary>
    /// Records against the history that an interview was scheduled. Called from the handler
    /// that created the interview aggregate, so the application's timeline stays complete
    /// without the two aggregates referencing each other's internals.
    /// </summary>
    public Result NoteInterviewScheduled(
        Guid entryId, DateTimeOffset scheduledFor, UserId? actorId, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.ApplicationNotFound);
        }

        LastActivityAt = now;

        Append(
            ApplicationEventKind.InterviewScheduled,
            fromStatus: null,
            toStatus: null,
            actorId,
            note: scheduledFor.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            isVisibleToCandidate: true,
            now,
            entryId);

        return Result.Success();
    }

    /// <summary>Records an interview outcome against the history.</summary>
    public Result NoteInterviewCompleted(
        Guid entryId, InterviewOutcome outcome, UserId? actorId, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.ApplicationNotFound);
        }

        LastActivityAt = now;

        Append(
            ApplicationEventKind.InterviewCompleted,
            fromStatus: null,
            toStatus: null,
            actorId,
            note: outcome.ToString(),

            // The outcome itself is the employer's. A candidate is told the decision through
            // the status change, not through a "Failed" line on their own timeline.
            isVisibleToCandidate: false,
            now,
            entryId);

        return Result.Success();
    }

    /// <summary>Records that terms went to the candidate.</summary>
    public Result NoteOfferExtended(Guid entryId, UserId? actorId, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.ApplicationNotFound);
        }

        LastActivityAt = now;

        Append(
            ApplicationEventKind.OfferExtended,
            fromStatus: null,
            toStatus: null,
            actorId,
            note: null,
            isVisibleToCandidate: true,
            now,
            entryId);

        return Result.Success();
    }

    /// <summary>Records the candidate's answer to an offer.</summary>
    public Result NoteOfferAnswered(
        Guid entryId, bool accepted, UserId? actorId, DateTimeOffset now)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.ApplicationNotFound);
        }

        LastActivityAt = now;

        Append(
            ApplicationEventKind.OfferAnswered,
            fromStatus: null,
            toStatus: null,
            actorId,
            note: accepted ? "accepted" : "rejected",
            isVisibleToCandidate: true,
            now,
            entryId);

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Marks the application deleted.
    /// <para>
    /// Soft, like everything else, but with a caveat worth stating: an application is
    /// personal data belonging to the candidate, and a genuine erasure request is a different
    /// operation from this one — it has to reach the attachments in Media too. This is the
    /// ordinary "remove it from my list" delete.
    /// </para>
    /// </summary>
    public void MarkDeleted(DateTimeOffset timestamp, string? userId)
    {
        IsDeleted = true;
        DeletedAt = timestamp;
        DeletedBy = userId;
    }

    public void Restore()
    {
        IsDeleted = false;
        DeletedAt = null;
        DeletedBy = null;
    }

    /// <summary>Stamped by the shared persistence interceptor. Never called by a handler.</summary>
    public void SetCreated(DateTimeOffset timestamp, string? userId)
    {
        CreatedAt = timestamp;
        CreatedBy = userId;
    }

    /// <inheritdoc cref="SetCreated" />
    public void SetModified(DateTimeOffset timestamp, string? userId)
    {
        ModifiedAt = timestamp;
        ModifiedBy = userId;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Internals
    // ════════════════════════════════════════════════════════════════════════════════════════

    private void Touch() => Revision++;

    private void Append(
        ApplicationEventKind kind,
        ApplicationStatus? fromStatus,
        ApplicationStatus? toStatus,
        UserId? actorId,
        string? note,
        bool isVisibleToCandidate,
        DateTimeOffset occurredAt,
        Guid entryId = default) =>
        _history.Add(ApplicationHistoryEntry.Create(
            entryId, kind, fromStatus, toStatus, actorId, note, isVisibleToCandidate, occurredAt));

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }

    private static string? NormalizeStageKey(string? stageKey)
    {
        if (string.IsNullOrWhiteSpace(stageKey))
        {
            return null;
        }

        var key = stageKey.Trim().ToLowerInvariant();

        var valid = key.Length <= JobLimits.MaxStageKey
            && key.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            && !key.StartsWith('-')
            && !key.EndsWith('-');

        return valid ? key : null;
    }
}
