using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Jobs.Domain.Enums;
using Diaspora.Modules.Jobs.Domain.Errors;
using Diaspora.Modules.Jobs.Domain.Events;
using Diaspora.Modules.Jobs.Domain.Identifiers;
using Diaspora.Modules.Jobs.Domain.Policies;
using Diaspora.Modules.Jobs.Domain.ValueObjects;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Jobs.Domain.Interviews;

/// <summary>
/// One interview round against one application.
/// <para>
/// <b>Its own aggregate rather than a collection on the application, and the round number is
/// why.</b> Real hiring runs several rounds, each with its own schedule, panel and outcome, and
/// each moving independently — a second-round interview is rescheduled without touching the
/// first. Nesting them inside the application would mean loading the whole hiring record to
/// move a calendar appointment, and would put concurrent reschedules of different rounds in
/// contention over one revision number.
/// </para>
/// <para>
/// <b>The platform hosts nothing.</b> Video interviewing is explicitly out of scope; a video
/// interview here is a link the employer supplies to whatever they already use. Automated
/// scoring is out of scope too, which is why the outcome is an enum a human sets and the
/// feedback is prose nobody parses.
/// </para>
/// <para>
/// <b>Feedback is internal by default.</b> <see cref="Feedback"/> is what the panel wrote and
/// is never shown to the candidate; <see cref="CandidateFeedback"/> is the version the employer
/// chose to share, and is usually empty. Keeping them in separate fields is the only way both
/// an honest internal note and a kind external one can exist.
/// </para>
/// </summary>
public sealed class Interview
    : AggregateRoot<InterviewId>, IAuditableEntity, ISoftDeletable, IHasOwner
{
    private readonly List<InterviewParticipant> _participants = [];

    private Interview(
        InterviewId id,
        JobApplicationId applicationId,
        JobPostingId postingId,
        EmployerProfileId employerId,
        CandidateProfileId candidateId,
        InterviewSchedule schedule,
        int roundNumber,
        DateTimeOffset createdAt)
        : base(id)
    {
        ApplicationId = applicationId;
        PostingId = postingId;
        EmployerId = employerId;
        CandidateId = candidateId;
        Schedule = schedule;
        RoundNumber = roundNumber;
        Outcome = InterviewOutcome.Pending;
        CreatedAt = createdAt;
    }

    private Interview() { }

    // ── Parties ─────────────────────────────────────────────────────────────────────────────

    public JobApplicationId ApplicationId { get; private set; } = default!;

    /// <summary>Denormalised, so an employer's interview calendar needs no join to filter by job.</summary>
    public JobPostingId PostingId { get; private set; } = default!;

    /// <summary>Denormalised, so the ownership check is a column comparison.</summary>
    public EmployerProfileId EmployerId { get; private set; } = default!;

    public CandidateProfileId CandidateId { get; private set; } = default!;

    /// <summary>The platform's ownership seam. The employer runs the interview.</summary>
    public Guid OwnerId => EmployerId.Value;

    // ── The appointment ─────────────────────────────────────────────────────────────────────

    public InterviewSchedule Schedule { get; private set; } = default!;

    /// <summary>Which round. First, second, final — a candidate deserves to know.</summary>
    public int RoundNumber { get; private set; }

    /// <summary>What the employer calls it. "Technical screen", "Panel with the ward team".</summary>
    public string? Title { get; private set; }

    public InterviewOutcome Outcome { get; private set; }

    /// <summary>Whether it was called off. Distinct from an outcome of Cancelled on a held slot.</summary>
    public bool IsCancelled { get; private set; }

    public DateTimeOffset? CancelledAt { get; private set; }

    public string? CancellationReason { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>The panel's notes. Internal. Never rendered to the candidate.</summary>
    public string? Feedback { get; private set; }

    /// <summary>The version the employer chose to share. Usually null.</summary>
    public string? CandidateFeedback { get; private set; }

    /// <summary>How many times it has been moved. Visible to the candidate, honestly.</summary>
    public int RescheduleCount { get; private set; }

    /// <summary>The optimistic concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<InterviewParticipant> Participants =>
        _participants.OrderBy(participant => participant.Order).ToList().AsReadOnly();

    // ── Auditing & soft delete ──────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public string? DeletedBy { get; private set; }

    public bool IsDeleted { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether the appointment is still ahead and still on.</summary>
    public bool IsUpcoming(DateTimeOffset now) =>
        !IsDeleted && !IsCancelled && Outcome == InterviewOutcome.Pending
        && Schedule.ScheduledAt > now;

    /// <summary>Whether it has happened but nobody has recorded what came of it.</summary>
    public bool AwaitsOutcome(DateTimeOffset now) =>
        !IsDeleted && !IsCancelled && Outcome == InterviewOutcome.Pending
        && Schedule.HasPassed(now);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Scheduling
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Schedules an interview round against an application.
    /// <para>
    /// Named <c>Arrange</c> rather than <c>Schedule</c> because the appointment itself is the
    /// <see cref="Schedule"/> property, and a static factory sharing that name would not
    /// compile. Arranging is what the employer does; a schedule is what results.
    /// </para>
    /// </summary>
    public static Result<Interview> Arrange(
        InterviewId id,
        JobApplicationId applicationId,
        JobPostingId postingId,
        EmployerProfileId employerId,
        CandidateProfileId candidateId,
        InterviewSchedule schedule,
        int roundNumber,
        string? title,
        IReadOnlyList<InterviewParticipantDraft> participants,
        DateTimeOffset now,
        UserId? scheduledBy = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(applicationId);
        ArgumentNullException.ThrowIfNull(postingId);
        ArgumentNullException.ThrowIfNull(employerId);
        ArgumentNullException.ThrowIfNull(candidateId);
        ArgumentNullException.ThrowIfNull(schedule);
        ArgumentNullException.ThrowIfNull(participants);

        if (roundNumber is < 1 or > JobLimits.MaxInterviewsPerApplication)
        {
            return Result.Failure<Interview>(JobErrors.InterviewLimit);
        }

        var interview = new Interview(
            id, applicationId, postingId, employerId, candidateId, schedule, roundNumber, now);

        var named = interview.ApplyTitle(title);

        if (named.IsFailure)
        {
            return Result.Failure<Interview>(named.Error);
        }

        var panelled = interview.SetParticipants(participants);

        if (panelled.IsFailure)
        {
            return Result.Failure<Interview>(panelled.Error);
        }

        interview.RaiseScheduled(scheduledBy, isReschedule: false);

        return Result.Success(interview);
    }

    /// <summary>
    /// Moves the appointment.
    /// <para>
    /// <see cref="RescheduleCount"/> is incremented and surfaced. An employer who has moved an
    /// interview four times has told the candidate something, and hiding it would be the
    /// platform helping them hide it.
    /// </para>
    /// </summary>
    public Result Reschedule(
        InterviewSchedule schedule, DateTimeOffset now, UserId? rescheduledBy = null)
    {
        ArgumentNullException.ThrowIfNull(schedule);

        var guard = EnsureOpen();

        if (guard.IsFailure)
        {
            return guard;
        }

        if (Schedule.Equals(schedule))
        {
            return Result.Success();
        }

        Schedule = schedule;
        RescheduleCount++;
        Touch();

        RaiseScheduled(rescheduledBy, isReschedule: true);

        return Result.Success();
    }

    /// <summary>Renames the round and replaces its panel.</summary>
    public Result UpdateDetails(
        string? title, IReadOnlyList<InterviewParticipantDraft> participants, int? baseRevision)
    {
        ArgumentNullException.ThrowIfNull(participants);

        var guard = EnsureOpen();

        if (guard.IsFailure)
        {
            return guard;
        }

        var revision = EnsureRevision(baseRevision);

        if (revision.IsFailure)
        {
            return revision;
        }

        var named = ApplyTitle(title);

        if (named.IsFailure)
        {
            return named;
        }

        var panelled = SetParticipants(participants);

        if (panelled.IsFailure)
        {
            return panelled;
        }

        Touch();

        return Result.Success();
    }

    /// <summary>Calls the interview off. Terminal — a new appointment is a new round.</summary>
    public Result Cancel(string? reason, DateTimeOffset now, UserId? cancelledBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.InterviewNotFound);
        }

        if (IsCancelled)
        {
            return Result.Success();
        }

        if (Outcome != InterviewOutcome.Pending)
        {
            return Result.Failure(JobErrors.InterviewAlreadyCompleted);
        }

        var wasScheduledAt = Schedule.ScheduledAt;

        IsCancelled = true;
        CancelledAt = now;
        CancellationReason = Clip(reason, JobLimits.MaxInterviewNote);
        Outcome = InterviewOutcome.Cancelled;
        Touch();

        RaiseDomainEvent(new InterviewCancelledDomainEvent
        {
            InterviewId = Id,
            ApplicationId = ApplicationId,
            CandidateId = CandidateId,
            WasScheduledAt = wasScheduledAt,
            Reason = CancellationReason,
            CancelledBy = cancelledBy,
            CancelledAt = now
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Outcome
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Records what the interview concluded.
    /// <para>
    /// Recordable before the scheduled end, because interviews finish early and a panel that
    /// has decided should not have to wait for a clock. Not recordable twice: an outcome that
    /// could be overwritten is not a record, and the honest way to change a decision is a new
    /// round.
    /// </para>
    /// </summary>
    public Result RecordOutcome(
        InterviewOutcome outcome,
        string? feedback,
        string? candidateFeedback,
        DateTimeOffset now,
        UserId? recordedBy = null)
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.InterviewNotFound);
        }

        if (IsCancelled)
        {
            return Result.Failure(JobErrors.InterviewCancelled);
        }

        if (Outcome != InterviewOutcome.Pending)
        {
            return Result.Failure(JobErrors.InterviewAlreadyCompleted);
        }

        if (!Enum.IsDefined(outcome) || outcome == InterviewOutcome.Pending)
        {
            return Result.Failure(JobErrors.InterviewNotScheduled);
        }

        // Cancelling is its own operation, with its own notification. Routing it through the
        // outcome would leave the candidate uninformed about an appointment they still hold.
        if (outcome == InterviewOutcome.Cancelled)
        {
            return Cancel(feedback, now, recordedBy);
        }

        Outcome = outcome;
        CompletedAt = now;
        Feedback = Clip(feedback, JobLimits.MaxInterviewNote);
        CandidateFeedback = Clip(candidateFeedback, JobLimits.MaxInterviewNote);
        Touch();

        RaiseDomainEvent(new InterviewOutcomeRecordedDomainEvent
        {
            InterviewId = Id,
            ApplicationId = ApplicationId,
            PostingId = PostingId,
            Outcome = outcome,
            RoundNumber = RoundNumber,
            RecordedBy = recordedBy,
            RecordedAt = now
        });

        return Result.Success();
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Concurrency, soft delete & auditing
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>Rejects a save based on a revision the caller no longer holds.</summary>
    public Result EnsureRevision(int? baseRevision) =>
        baseRevision is { } expected && expected != Revision
            ? Result.Failure(JobErrors.RevisionConflict)
            : Result.Success();

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

    private Result EnsureOpen()
    {
        if (IsDeleted)
        {
            return Result.Failure(JobErrors.InterviewNotFound);
        }

        if (IsCancelled)
        {
            return Result.Failure(JobErrors.InterviewCancelled);
        }

        return Outcome != InterviewOutcome.Pending
            ? Result.Failure(JobErrors.InterviewAlreadyCompleted)
            : Result.Success();
    }

    private Result ApplyTitle(string? title)
    {
        var trimmed = Clip(title, JobLimits.MaxDisplayName);
        Title = trimmed;

        return Result.Success();
    }

    private Result SetParticipants(IReadOnlyList<InterviewParticipantDraft> participants)
    {
        if (participants.Count > JobLimits.MaxInterviewers)
        {
            return Result.Failure(JobErrors.InterviewLimit);
        }

        var rebuilt = new List<InterviewParticipant>(participants.Count);

        foreach (var draft in participants)
        {
            if (draft is null)
            {
                return Result.Failure(JobErrors.InterviewLocationInvalid);
            }

            var participant = InterviewParticipant.Create(
                draft.ParticipantId,
                draft.UserId is { } userId && userId != Guid.Empty ? UserId.From(userId) : null,
                draft.Name,
                draft.Role,
                rebuilt.Count);

            if (participant is null)
            {
                return Result.Failure(JobErrors.InterviewLocationInvalid);
            }

            rebuilt.Add(participant);
        }

        _participants.Clear();
        _participants.AddRange(rebuilt);

        return Result.Success();
    }

    private void RaiseScheduled(UserId? actorId, bool isReschedule) =>
        RaiseDomainEvent(new InterviewScheduledDomainEvent
        {
            InterviewId = Id,
            ApplicationId = ApplicationId,
            PostingId = PostingId,
            EmployerId = EmployerId,
            CandidateId = CandidateId,
            Mode = Schedule.Mode,
            ScheduledAt = Schedule.ScheduledAt,
            DurationMinutes = Schedule.DurationMinutes,
            TimeZoneId = Schedule.TimeZoneId,
            RoundNumber = RoundNumber,
            IsReschedule = isReschedule,
            ScheduledBy = actorId
        });

    private static string? Clip(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        return trimmed.Length <= max ? trimmed : trimmed[..max].TrimEnd();
    }
}
