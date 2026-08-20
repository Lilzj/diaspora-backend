using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Verification;

/// <summary>
/// One member's application to the creator programme.
/// <para>
/// <b>This is not identity verification and shares nothing with it.</b> Trust owns whether somebody
/// is who they say they are, and this aggregate stores none of that: no document, no number, no
/// image, no expiry of a passport. Identity verification is a <em>requirement</em> of this
/// application, checked against <c>ITrustService</c> at submission and rendered as an unmet
/// requirement pointing at Trust's own flow until it is satisfied. The register: "Trust verifies
/// people, not programmes."
/// </para>
/// <para>
/// <b>Evidence is a server-issued reference and a server-produced label. Nothing else.</b> The
/// register: "No file content, no local URI, no document number — the client never holds them,
/// which is how the guarantee is kept rather than promised." The same rule holds server-side: this
/// aggregate stores what a reviewer needs to find the material, not the material.
/// </para>
/// <para>
/// <b>Every state change is recorded, including who made it.</b> §30 of the phase brief asks for
/// auditable decisions with the previous state, the new state and the actor; <see cref="Decisions"/>
/// is that record, and it is never served on the member's own read.
/// </para>
/// <para>
/// <b>One live application per creator, enforced by a unique index.</b> A second row would let two
/// reviewers decide two different answers to one question.
/// </para>
/// </summary>
public sealed class CreatorVerificationApplication
    : AggregateRoot<CreatorVerificationApplicationId>, IAuditableEntity
{
    private readonly List<CreatorVerificationEvidence> _evidence = [];
    private readonly List<AcknowledgedRequirement> _acknowledged = [];
    private readonly List<InformationRequest> _informationRequests = [];
    private readonly List<VerificationDecisionRecord> _decisions = [];

    private CreatorVerificationApplication(
        CreatorVerificationApplicationId id,
        CreatorId creatorId,
        Guid userId,
        DateTimeOffset startedAt) : base(id)
    {
        CreatorId = creatorId;
        UserId = userId;
        State = CreatorVerificationState.Draft;
        StartedAt = startedAt;
        CreatedAt = startedAt;
    }

    private CreatorVerificationApplication() { }

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid UserId { get; private set; }

    public CreatorVerificationState State { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? ReviewStartedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>When an approval lapses. Null in every other state.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    /// <summary>How long the member has to answer a reviewer's request.</summary>
    public DateTimeOffset? RespondByAt { get; private set; }

    /// <summary>The reviewer's own words, shown to the member verbatim.</summary>
    public string? DecisionReason { get; private set; }

    /// <summary>What the member said when they submitted.</summary>
    public string? Note { get; private set; }

    /// <summary>
    /// One appeal per refusal, and the flag rather than a count because the register's machine
    /// draws exactly one arrow out of Rejected.
    /// </summary>
    public bool HasAppealed { get; private set; }

    /// <summary>
    /// The submission's idempotency key. Uniquely indexed, so two submits carrying one key produce
    /// one submission and the second replays the first's answer.
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    public IReadOnlyList<CreatorVerificationEvidence> Evidence => _evidence.AsReadOnly();

    public IReadOnlyList<AcknowledgedRequirement> Acknowledged => _acknowledged.AsReadOnly();

    public IReadOnlyList<InformationRequest> InformationRequests => _informationRequests.AsReadOnly();

    /// <summary>The audit trail. Never served on a member endpoint.</summary>
    public IReadOnlyList<VerificationDecisionRecord> Decisions => _decisions.AsReadOnly();

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static CreatorVerificationApplication Start(
        CreatorVerificationApplicationId id,
        CreatorId creatorId,
        Guid userId,
        DateTimeOffset startedAt) =>
        new(id, creatorId, userId, startedAt);

    /// <summary>
    /// Attaches a piece of supporting material by reference.
    /// <para>
    /// Only in an editable state, and bounded. A caller who could attach evidence to an application
    /// already with a reviewer could change what was being reviewed after it was picked up.
    /// </para>
    /// </summary>
    public Result<CreatorVerificationEvidence> AttachEvidence(
        Guid evidenceId, string kind, string label, DateTimeOffset now)
    {
        if (!CreatorVerificationTransitions.IsEditable(State))
        {
            return Result.Failure<CreatorVerificationEvidence>(
                Errors.CreatorErrors.VerificationInvalidTransition);
        }

        if (_evidence.Count >= CreatorLimits.MaxEvidenceItems)
        {
            return Result.Failure<CreatorVerificationEvidence>(
                Errors.CreatorErrors.EvidenceLimitReached);
        }

        var attached = CreatorVerificationEvidence.Create(evidenceId, kind, label, now);

        _evidence.Add(attached);
        Touch(now);

        return Result.Success(attached);
    }

    /// <summary>
    /// The member hands the application in.
    /// <para>
    /// <b>The acknowledged requirements are recorded, not trusted.</b> A member asserting they have
    /// met the identity requirement does not make it met — the submission handler asks Trust — and
    /// this list exists so a reviewer can see what the member believed rather than so the platform
    /// can skip a check.
    /// </para>
    /// </summary>
    public Result Submit(
        IEnumerable<string> acknowledgedRequirements,
        IEnumerable<Guid> evidenceIds,
        string? note,
        string? idempotencyKey,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(acknowledgedRequirements);
        ArgumentNullException.ThrowIfNull(evidenceIds);

        if (!CreatorVerificationTransitions.IsAllowed(State, CreatorVerificationState.Submitted))
        {
            return Result.Failure(Errors.CreatorErrors.VerificationInvalidTransition);
        }

        var referenced = evidenceIds.ToHashSet();

        // Evidence the member did not name on the submission is dropped rather than carried
        // silently: what is being reviewed should be what was handed in.
        _evidence.RemoveAll(item => referenced.Count > 0 && !referenced.Contains(item.EvidenceId));

        _acknowledged.Clear();

        foreach (var key in acknowledgedRequirements
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Select(key => key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(CreatorLimits.MaxEvidenceItems * 2))
        {
            _acknowledged.Add(AcknowledgedRequirement.Create(key));
        }

        if (!string.IsNullOrWhiteSpace(note))
        {
            Note = note.Trim()[..Math.Min(note.Trim().Length, CreatorLimits.MaxNoteLength)];
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            IdempotencyKey = idempotencyKey;
        }

        Record(State, CreatorVerificationState.Submitted, UserId, null, now);

        State = CreatorVerificationState.Submitted;
        SubmittedAt = now;
        RespondByAt = null;
        Touch(now);

        return Result.Success();
    }

    /// <summary>A reviewer picks the application up.</summary>
    public Result BeginReview(Guid reviewerUserId, DateTimeOffset now) =>
        Move(CreatorVerificationState.UnderReview, reviewerUserId, null, now, application =>
        {
            application.ReviewStartedAt = now;
        });

    /// <summary>A reviewer asks for something, with a deadline the member can see.</summary>
    public Result RequestInformation(
        Guid reviewerUserId,
        IEnumerable<string> requests,
        DateTimeOffset? respondBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(requests);

        return Move(
            CreatorVerificationState.MoreInformationRequired, reviewerUserId, null, now, application =>
            {
                application._informationRequests.Clear();

                foreach (var request in requests
                    .Where(request => !string.IsNullOrWhiteSpace(request))
                    .Select(request => request.Trim())
                    .Take(CreatorLimits.MaxInformationRequests))
                {
                    application._informationRequests.Add(InformationRequest.Create(request, now));
                }

                application.RespondByAt = respondBy;
            });
    }

    /// <summary>The member reopens the application to answer a reviewer.</summary>
    public Result ReturnToDraft(DateTimeOffset now) =>
        Move(CreatorVerificationState.Draft, UserId, null, now);

    /// <summary>
    /// Approved. The standing lasts a year, which is why it carries an expiry rather than being
    /// permanent — see <c>CreatorLimits.ApprovalValidDays</c>.
    /// </summary>
    public Result Approve(Guid reviewerUserId, string? reason, DateTimeOffset now) =>
        Move(CreatorVerificationState.Approved, reviewerUserId, reason, now, application =>
        {
            application.DecidedAt = now;
            application.DecisionReason = Trim(reason);
            application.ExpiresAt = now.AddDays(CreatorLimits.ApprovalValidDays);
            application.RespondByAt = null;
        });

    /// <summary>Refused, with the reviewer's words shown verbatim.</summary>
    public Result Reject(Guid reviewerUserId, string? reason, DateTimeOffset now) =>
        Move(CreatorVerificationState.Rejected, reviewerUserId, reason, now, application =>
        {
            application.DecidedAt = now;
            application.DecisionReason = Trim(reason);
            application.ExpiresAt = null;
            application.RespondByAt = null;
        });

    /// <summary>The member appeals a refusal. Once.</summary>
    public Result Appeal(string? reason, DateTimeOffset now)
    {
        if (HasAppealed)
        {
            return Result.Failure(Errors.CreatorErrors.VerificationCannotAppeal);
        }

        var moved = Move(CreatorVerificationState.AppealPending, UserId, reason, now, application =>
        {
            application.HasAppealed = true;
            application.Note = Trim(reason) ?? application.Note;
        });

        return moved;
    }

    /// <summary>The platform withdrawing somebody from the programme. Reachable from anywhere.</summary>
    public Result Suspend(Guid actorUserId, string? reason, DateTimeOffset now) =>
        Move(CreatorVerificationState.Suspended, actorUserId, reason, now, application =>
        {
            application.DecisionReason = Trim(reason);
            application.ExpiresAt = null;
        });

    /// <summary>An approval lapsed. Driven by the clock rather than by a person.</summary>
    public Result Expire(DateTimeOffset now) =>
        Move(CreatorVerificationState.Expired, actorUserId: null, null, now, application =>
        {
            application.ExpiresAt = null;
        });

    /// <summary>Whether an approval has run out. The sweep's predicate, in the domain.</summary>
    public bool IsApprovalLapsed(DateTimeOffset now) =>
        State == CreatorVerificationState.Approved && ExpiresAt is { } expiry && expiry <= now;

    public bool CanResubmit =>
        CreatorVerificationTransitions.IsAllowed(State, CreatorVerificationState.Submitted)
        || State == CreatorVerificationState.MoreInformationRequired;

    public bool CanAppeal => State == CreatorVerificationState.Rejected && !HasAppealed;

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

    private Result Move(
        CreatorVerificationState target,
        Guid? actorUserId,
        string? reason,
        DateTimeOffset now,
        Action<CreatorVerificationApplication>? mutate = null)
    {
        if (!CreatorVerificationTransitions.IsAllowed(State, target))
        {
            return Result.Failure(Errors.CreatorErrors.VerificationInvalidTransition);
        }

        var previous = State;

        Record(previous, target, actorUserId, reason, now);

        State = target;
        mutate?.Invoke(this);
        Touch(now);

        // Only a decided outcome is announced. A reviewer picking the application up and the member
        // reopening it are steps the member is already watching on screen.
        if (target is CreatorVerificationState.Approved
            or CreatorVerificationState.Rejected
            or CreatorVerificationState.Suspended
            or CreatorVerificationState.Expired)
        {
            RaiseDomainEvent(new CreatorVerificationDecidedDomainEvent(
                Id, CreatorId, UserId, previous, target, Trim(reason), now));
        }

        return Result.Success();
    }

    private void Record(
        CreatorVerificationState from,
        CreatorVerificationState to,
        Guid? actorUserId,
        string? reason,
        DateTimeOffset now) =>
        _decisions.Add(VerificationDecisionRecord.Create(
            Guid.NewGuid(), from, to, actorUserId, Trim(reason), now));

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}

/// <summary>
/// A piece of supporting material, by reference.
/// <para>
/// <b>Note what is absent.</b> No file contents, no base64, no local URI, no document number.
/// <see cref="EvidenceId"/> is server-issued and <see cref="Label"/> is server-produced; a reviewer
/// resolves the reference through the module that holds the material, and this row never does.
/// </para>
/// </summary>
public sealed class CreatorVerificationEvidence : ValueObject
{
    private CreatorVerificationEvidence(
        Guid evidenceId, string kind, string label, DateTimeOffset uploadedAt)
    {
        EvidenceId = evidenceId;
        Kind = kind;
        Label = label;
        Status = CreatorEvidenceStatus.Pending;
        UploadedAt = uploadedAt;
    }

    private CreatorVerificationEvidence() { }

    /// <summary>Server-issued. Caller-supplied to EF, so <c>ValueGeneratedNever()</c>.</summary>
    public Guid EvidenceId { get; private set; }

    /// <summary>What kind of thing it is — "portfolio", "publication", "endorsement".</summary>
    public string Kind { get; private set; } = string.Empty;

    /// <summary>A label the server produced. Never a filename the client chose.</summary>
    public string Label { get; private set; } = string.Empty;

    public CreatorEvidenceStatus Status { get; private set; }

    public string? RejectionReason { get; private set; }

    public DateTimeOffset UploadedAt { get; private set; }

    public static CreatorVerificationEvidence Create(
        Guid evidenceId, string kind, string label, DateTimeOffset uploadedAt) =>
        new(evidenceId,
            string.IsNullOrWhiteSpace(kind) ? "supporting" : kind.Trim(),
            string.IsNullOrWhiteSpace(label) ? "Supporting material" : label.Trim(),
            uploadedAt);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return EvidenceId;
    }
}

/// <summary>A requirement the member asserted they had met. Recorded, never trusted.</summary>
public sealed class AcknowledgedRequirement : ValueObject
{
    private AcknowledgedRequirement(string key) => Key = key;

    private AcknowledgedRequirement() { }

    public string Key { get; private set; } = string.Empty;

    public static AcknowledgedRequirement Create(string key) => new(key.Trim());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Key;
    }
}

/// <summary>Something a reviewer asked the member for.</summary>
public sealed class InformationRequest : ValueObject
{
    private InformationRequest(string text, DateTimeOffset requestedAt)
    {
        Text = text;
        RequestedAt = requestedAt;
    }

    private InformationRequest() { }

    public string Text { get; private set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; private set; }

    public static InformationRequest Create(string text, DateTimeOffset requestedAt) =>
        new(text.Trim(), requestedAt);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Text;
        yield return RequestedAt;
    }
}

/// <summary>
/// One entry in the application's audit trail.
/// <para>
/// §34 of the phase brief: who changed it, when, from what, to what, and why. Never served on a
/// member endpoint — an applicant reading which reviewer refused them is a safety problem rather
/// than a transparency win.
/// </para>
/// </summary>
public sealed class VerificationDecisionRecord : ValueObject
{
    private VerificationDecisionRecord(
        Guid recordId,
        CreatorVerificationState previousState,
        CreatorVerificationState currentState,
        Guid? actorUserId,
        string? reason,
        DateTimeOffset occurredAt)
    {
        RecordId = recordId;
        PreviousState = previousState;
        CurrentState = currentState;
        ActorUserId = actorUserId;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    private VerificationDecisionRecord() { }

    public Guid RecordId { get; private set; }

    public CreatorVerificationState PreviousState { get; private set; }

    public CreatorVerificationState CurrentState { get; private set; }

    /// <summary>Null when the clock did it rather than a person.</summary>
    public Guid? ActorUserId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static VerificationDecisionRecord Create(
        Guid recordId,
        CreatorVerificationState previousState,
        CreatorVerificationState currentState,
        Guid? actorUserId,
        string? reason,
        DateTimeOffset occurredAt) =>
        new(recordId, previousState, currentState, actorUserId, reason, occurredAt);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RecordId;
    }
}
