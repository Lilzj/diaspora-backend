using Diaspora.Domain.Abstractions;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Policies;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityProgramme.Domain.Volunteering;

/// <summary>
/// Somebody offering to do a piece of volunteer work.
/// <para>
/// Its own aggregate rather than a collection on the opportunity, and the reason is the one B12
/// gives for memberships: a well-publicised opportunity has hundreds of applicants, and loading them
/// to render the opportunity's title would be absurd. It is also what lets an organizer decide one
/// application without taking a write lock on every other applicant.
/// </para>
/// <para>
/// <b>A full opportunity waitlists rather than refuses</b> — the kinder of the two correct answers,
/// and the reason <see cref="VolunteerApplicationStatus.Waitlisted"/> is a pending state rather than
/// a terminal one. The decision is made by the handler from the opportunity's counters, because it
/// is a fact about the opportunity rather than about this row.
/// </para>
/// </summary>
public sealed class VolunteerApplication
    : AggregateRoot<VolunteerApplicationId>, IAuditableEntity
{
    private readonly List<ApplicationAnswer> _answers = [];

    private VolunteerApplication(
        VolunteerApplicationId id,
        VolunteerOpportunityId opportunityId,
        Guid organizationId,
        UserId volunteerUserId,
        VolunteerShiftId? shiftId,
        VolunteerApplicationStatus status,
        DateTimeOffset now) : base(id)
    {
        OpportunityId = opportunityId;
        OrganizationId = organizationId;
        VolunteerUserId = volunteerUserId;
        ShiftId = shiftId;
        Status = status;
        CreatedAt = now;
        SubmittedAt = status == VolunteerApplicationStatus.Draft ? null : now;
    }

    private VolunteerApplication() { }

    public VolunteerOpportunityId OpportunityId { get; private set; } = default!;

    /// <summary>
    /// Copied from the opportunity at submission.
    /// <para>
    /// <b>A denormalisation, and a deliberate one.</b> Every authorization check on this row asks
    /// "may the caller manage this organization's volunteers", and reading the opportunity to find
    /// out which organization it belongs to would make that two queries on the hottest path in the
    /// module. An opportunity cannot change organization, so the copy cannot go stale.
    /// </para>
    /// </summary>
    public Guid OrganizationId { get; private set; }

    public UserId VolunteerUserId { get; private set; } = default!;

    /// <summary>The shift applied for, when the opportunity has shifts.</summary>
    public VolunteerShiftId? ShiftId { get; private set; }

    public VolunteerApplicationStatus Status { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public UserId? DecidedByUserId { get; private set; }

    /// <summary>Surfaced to the applicant verbatim.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>
    /// Where in the queue, when waitlisted.
    /// <para>
    /// Computed at read time from the submission order of the other waitlisted applications, because
    /// a stored position goes wrong the moment somebody ahead withdraws — and a member told they are
    /// fourth when they are second has been told something false.
    /// </para>
    /// </summary>
    public DateTimeOffset? WaitlistedAt { get; private set; }

    /// <summary>B15's idempotency primitive. Uniquely indexed.</summary>
    public string? IdempotencyKey { get; private set; }

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public IReadOnlyList<ApplicationAnswer> Answers => _answers.AsReadOnly();

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    // ── Derived reads ───────────────────────────────────────────────────────────────────────

    /// <summary>Whether the application still occupies a place on the opportunity.</summary>
    public bool OccupiesPlace => Status is VolunteerApplicationStatus.Approved
        or VolunteerApplicationStatus.Completed;

    public bool IsWaiting => Status == VolunteerApplicationStatus.Waitlisted;

    public bool IsPending => Status is VolunteerApplicationStatus.Pending
        or VolunteerApplicationStatus.Waitlisted;

    public bool IsTerminal => Status is VolunteerApplicationStatus.Rejected
        or VolunteerApplicationStatus.Withdrawn
        or VolunteerApplicationStatus.Cancelled
        or VolunteerApplicationStatus.Completed;

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Creation
    // ════════════════════════════════════════════════════════════════════════════════════════

    public static Result<VolunteerApplication> Submit(
        VolunteerApplicationId id,
        VolunteerOpportunityId opportunityId,
        Guid organizationId,
        UserId volunteerUserId,
        VolunteerShiftId? shiftId,
        VolunteerApplicationStatus openingStatus,
        DateTimeOffset now,
        IEnumerable<ApplicationAnswer>? answers = null,
        string? idempotencyKey = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(opportunityId);
        ArgumentNullException.ThrowIfNull(volunteerUserId);

        if (openingStatus is not (VolunteerApplicationStatus.Draft
            or VolunteerApplicationStatus.Pending
            or VolunteerApplicationStatus.Waitlisted))
        {
            return Result.Failure<VolunteerApplication>(
                CommunityProgrammeErrors.ApplicationTransitionInvalid);
        }

        var application = new VolunteerApplication(
            id, opportunityId, organizationId, volunteerUserId, shiftId, openingStatus, now)
        {
            IdempotencyKey = MembershipProgramme.Clip(
                idempotencyKey, CommunityProgrammeLimits.MaxIdempotencyKeyLength),
            WaitlistedAt = openingStatus == VolunteerApplicationStatus.Waitlisted ? now : null
        };

        if (answers is not null)
        {
            application._answers.AddRange(answers.Take(CommunityProgrammeLimits.MaxAnswers));
        }

        return Result.Success(application);
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Lifecycle
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// The one lifecycle move. Raises the decision event only for the transitions that are one.
    /// <para>
    /// A withdrawal is the applicant's own act and is not a decision — publishing it as one would
    /// send the applicant a notification telling them what they just did.
    /// </para>
    /// </summary>
    public Result MoveTo(
        VolunteerApplicationStatus target,
        DateTimeOffset now,
        UserId? decidedBy = null,
        string? reason = null)
    {
        if (Status == target)
        {
            return Result.Success();
        }

        if (!CanTransition(Status, target))
        {
            return Result.Failure(CommunityProgrammeErrors.ApplicationTransitionInvalid);
        }

        var previous = Status;

        Status = target;
        Revision++;
        ModifiedAt = now;

        switch (target)
        {
            case VolunteerApplicationStatus.Pending:
                SubmittedAt ??= now;
                WaitlistedAt = null;
                break;

            case VolunteerApplicationStatus.Waitlisted:
                WaitlistedAt ??= now;
                break;

            case VolunteerApplicationStatus.Approved:
            case VolunteerApplicationStatus.Rejected:
                DecidedAt = now;
                DecidedByUserId = decidedBy;
                DecisionReason = MembershipProgramme.Clip(
                    reason, CommunityProgrammeLimits.MaxReasonLength);
                WaitlistedAt = null;
                break;

            case VolunteerApplicationStatus.Withdrawn:
            case VolunteerApplicationStatus.Cancelled:
                WaitlistedAt = null;

                // The applicant's own words were written for an organizer who is no longer deciding.
                // Keeping them serves nobody and is the kind of text somebody would ask to have
                // forgotten.
                _answers.Clear();
                break;
        }

        var isDecision = target is VolunteerApplicationStatus.Approved
            or VolunteerApplicationStatus.Rejected
            or VolunteerApplicationStatus.Waitlisted;

        if (isDecision && decidedBy is not null)
        {
            RaiseDomainEvent(new VolunteerApplicationDecidedDomainEvent
            {
                ApplicationId = Id,
                OpportunityId = OpportunityId,
                OrganizationId = OrganizationId,
                VolunteerUserId = VolunteerUserId,
                PreviousStatus = previous,
                CurrentStatus = target,
                Reason = reason,
                DecidedAt = now
            });
        }

        return Result.Success();
    }

    private static bool CanTransition(
        VolunteerApplicationStatus from, VolunteerApplicationStatus to) => from switch
        {
            VolunteerApplicationStatus.Draft =>
                to is VolunteerApplicationStatus.Pending or VolunteerApplicationStatus.Cancelled,

            VolunteerApplicationStatus.Pending =>
                to is VolunteerApplicationStatus.Approved
                    or VolunteerApplicationStatus.Rejected
                    or VolunteerApplicationStatus.Waitlisted
                    or VolunteerApplicationStatus.Withdrawn
                    or VolunteerApplicationStatus.Cancelled,

            // A place freeing up promotes a waitlisted applicant. That is the whole reason
            // waitlisting is kinder than refusing.
            VolunteerApplicationStatus.Waitlisted =>
                to is VolunteerApplicationStatus.Approved
                    or VolunteerApplicationStatus.Rejected
                    or VolunteerApplicationStatus.Withdrawn
                    or VolunteerApplicationStatus.Cancelled,

            VolunteerApplicationStatus.Approved =>
                to is VolunteerApplicationStatus.Completed
                    or VolunteerApplicationStatus.Withdrawn
                    or VolunteerApplicationStatus.Cancelled,

            _ => false
        };

    // ── Auditing ────────────────────────────────────────────────────────────────────────────

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

/// <summary>One answer to one of an opportunity's questions.</summary>
public sealed class ApplicationAnswer
{
    private ApplicationAnswer(Guid id, string question, string answer)
    {
        Id = id;
        Question = question;
        Answer = answer;
    }

    private ApplicationAnswer() { }

    public Guid Id { get; private set; }

    public string Question { get; private set; } = string.Empty;

    public string Answer { get; private set; } = string.Empty;

    public static ApplicationAnswer Create(string question, string answer) =>
        new(
            Guid.NewGuid(),
            MembershipProgramme.Clip(question, CommunityProgrammeLimits.MaxAnswerKeyLength)
                ?? string.Empty,
            MembershipProgramme.Clip(answer, CommunityProgrammeLimits.MaxAnswerValueLength)
                ?? string.Empty);
}
