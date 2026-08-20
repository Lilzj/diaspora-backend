using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Events;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.SharedKernel.Primitives;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Creator.Domain.Grants;

/// <summary>
/// One creator's application to one grant.
/// <para>
/// <b>Every decision on this aggregate is the server's.</b> §10 of the phase brief: "The frontend
/// must never decide approval, rejection, eligibility, grant award or grant completion." The state
/// machine is <c>GrantApplicationTransitions</c>, the four deciding moves need a reviewer permission
/// the applicant does not hold, and <see cref="Decisions"/> records who made each one.
/// </para>
/// <para>
/// <b>Answers are stored against the form's own field keys.</b> A key that is not on the form is
/// dropped rather than stored, so a client cannot smuggle a field into an application the reviewer
/// will never look at — and a required field that is missing refuses the submission rather than the
/// draft, because a draft is a work in progress and a submission is a claim.
/// </para>
/// <para>
/// <b>An awarded amount is recorded here and moved by B15.</b> Nothing in this file touches a
/// wallet, a ledger or a payment; the decision publishes <c>grant.application.decided</c> carrying
/// the amount and the currency, and the funding leg is B15's. See
/// <c>Application.Financial.GrantFundingBoundary</c>.
/// </para>
/// </summary>
public sealed class GrantApplication : AggregateRoot<GrantApplicationId>, IAuditableEntity
{
    private readonly List<GrantAnswer> _answers = [];
    private readonly List<GrantAttachment> _attachments = [];
    private readonly List<GrantAcceptedDeclaration> _acceptedDeclarations = [];
    private readonly List<GrantReport> _reports = [];
    private readonly List<GrantInformationRequest> _informationRequests = [];
    private readonly List<GrantDecisionRecord> _decisions = [];

    private GrantApplication(
        GrantApplicationId id,
        CommunityGrantId grantId,
        CreatorId creatorId,
        Guid applicantUserId,
        DateTimeOffset startedAt) : base(id)
    {
        GrantId = grantId;
        CreatorId = creatorId;
        ApplicantUserId = applicantUserId;
        State = GrantApplicationState.Draft;
        CreatedAt = startedAt;
    }

    private GrantApplication() { }

    public CommunityGrantId GrantId { get; private set; } = default!;

    public CreatorId CreatorId { get; private set; } = default!;

    public Guid ApplicantUserId { get; private set; }

    public GrantApplicationState State { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    public string? DecisionReason { get; private set; }

    public DateTimeOffset? RespondByAt { get; private set; }

    /// <summary>What the applicant asked for, where the programme lets them ask.</summary>
    public long? RequestedMinorUnits { get; private set; }

    /// <summary>What was actually awarded. Set on approval, by a reviewer, never by the applicant.</summary>
    public long? AwardedMinorUnits { get; private set; }

    public string? Currency { get; private set; }

    /// <summary>
    /// The submission's idempotency key. Uniquely indexed — two submits carrying one key produce
    /// one submission.
    /// </summary>
    public string? SubmitIdempotencyKey { get; private set; }

    /// <summary>
    /// B15's own handle for the funding movement, recorded when finance confirms it. Null until
    /// then, which is what makes "approved but not yet funded" a state the platform can see.
    /// </summary>
    public string? FundingReference { get; private set; }

    public IReadOnlyList<GrantAnswer> Answers => _answers.AsReadOnly();

    public IReadOnlyList<GrantAttachment> Attachments => _attachments.AsReadOnly();

    public IReadOnlyList<GrantAcceptedDeclaration> AcceptedDeclarations =>
        _acceptedDeclarations.AsReadOnly();

    public IReadOnlyList<GrantReport> Reports => _reports.AsReadOnly();

    public IReadOnlyList<GrantInformationRequest> InformationRequests =>
        _informationRequests.AsReadOnly();

    /// <summary>The audit trail. Never served on an applicant endpoint.</summary>
    public IReadOnlyList<GrantDecisionRecord> Decisions => _decisions.AsReadOnly();

    /// <summary>Bumped on every change. The optimistic-concurrency token.</summary>
    public int Revision { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public string? CreatedBy { get; private set; }

    public DateTimeOffset? ModifiedAt { get; private set; }

    public string? ModifiedBy { get; private set; }

    public static GrantApplication Start(
        GrantApplicationId id,
        CommunityGrantId grantId,
        CreatorId creatorId,
        Guid applicantUserId,
        DateTimeOffset startedAt) =>
        new(id, grantId, creatorId, applicantUserId, startedAt);

    /// <summary>
    /// Saves the applicant's answers.
    /// <para>
    /// Only while editable. Keys not on the form are dropped; values are trimmed and bounded. The
    /// <em>completeness</em> of the answers is checked at submission rather than here, because a
    /// draft that refused to save until it was finished would be a draft nobody could keep.
    /// </para>
    /// </summary>
    public Result SaveAnswers(
        CommunityGrant grant,
        IReadOnlyDictionary<string, string>? answers,
        long? requestedMinorUnits,
        IEnumerable<string>? attachmentIds,
        IEnumerable<string>? acceptedDeclarations,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grant);

        if (!GrantApplicationTransitions.IsEditable(State))
        {
            return Result.Failure(CreatorErrors.GrantApplicationNotEditable);
        }

        if (answers is not null)
        {
            _answers.Clear();

            foreach (var (key, value) in answers.Take(CreatorLimits.MaxFormFields))
            {
                if (grant.FindField(key) is not { } field)
                {
                    continue;
                }

                var trimmed = (value ?? string.Empty).Trim();

                _answers.Add(GrantAnswer.Create(
                    field.Key,
                    trimmed[..Math.Min(trimmed.Length, CreatorLimits.MaxAnswerLength)]));
            }
        }

        if (requestedMinorUnits is { } requested && grant.AllowsAmountRequest)
        {
            RequestedMinorUnits = requested;
            Currency = grant.Currency;
        }

        if (attachmentIds is not null && grant.AllowsAttachments)
        {
            _attachments.Clear();

            foreach (var reference in attachmentIds
                .Where(reference => !string.IsNullOrWhiteSpace(reference))
                .Select(reference => reference.Trim())
                .Distinct(StringComparer.Ordinal)
                .Take(CreatorLimits.MaxAttachments))
            {
                _attachments.Add(GrantAttachment.Create(reference, now));
            }
        }

        if (acceptedDeclarations is not null)
        {
            _acceptedDeclarations.Clear();

            foreach (var key in acceptedDeclarations
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(CreatorLimits.MaxDeclarations))
            {
                _acceptedDeclarations.Add(GrantAcceptedDeclaration.Create(key));
            }
        }

        Touch(now);

        return Result.Success();
    }

    /// <summary>
    /// The applicant hands the application in.
    /// <para>
    /// <b>Validated against the grant's own form rather than against anything the client sent.</b>
    /// Required fields, select values, numeric fields, the declarations and the requested range are
    /// all checked here, so a client that skipped its own validation cannot submit a half-formed
    /// application and a client that invented a field cannot smuggle one in.
    /// </para>
    /// </summary>
    public Result Submit(CommunityGrant grant, string? idempotencyKey, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grant);

        if (!GrantApplicationTransitions.IsAllowed(State, GrantApplicationState.Submitted))
        {
            return Result.Failure(CreatorErrors.GrantApplicationInvalidTransition);
        }

        var validated = Validate(grant);

        if (validated.IsFailure)
        {
            return validated;
        }

        var previous = State;

        Record(previous, GrantApplicationState.Submitted, ApplicantUserId, null, now);

        State = GrantApplicationState.Submitted;
        SubmittedAt = now;
        RespondByAt = null;

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            SubmitIdempotencyKey = idempotencyKey;
        }

        Touch(now);

        return Result.Success();
    }

    /// <summary>
    /// Checks the answers against the served form.
    /// <para>
    /// Public because the submit handler runs it before staging anything, so a refusal costs no
    /// write — and because a test asserting "a select field refuses a value not on the list" should
    /// be able to ask the domain rather than the HTTP surface.
    /// </para>
    /// </summary>
    public Result Validate(CommunityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        foreach (var field in grant.Fields)
        {
            var answer = _answers.Find(candidate =>
                string.Equals(candidate.FieldKey, field.Key, StringComparison.OrdinalIgnoreCase));

            var value = answer?.Value;

            if (string.IsNullOrWhiteSpace(value))
            {
                if (field.IsRequired)
                {
                    return Result.Failure(CreatorErrors.GrantFieldRequired);
                }

                continue;
            }

            if (field.MaxLength is { } maxLength && value.Length > maxLength)
            {
                return Result.Failure(CreatorErrors.GrantFieldInvalid);
            }

            switch (field.Kind)
            {
                case GrantFieldKind.Number
                    when !long.TryParse(
                        value,
                        System.Globalization.NumberStyles.Integer,
                        System.Globalization.CultureInfo.InvariantCulture,
                        out _):
                    return Result.Failure(CreatorErrors.GrantFieldInvalid);

                case GrantFieldKind.Select
                    when !grant.OptionsFor(field.Key).Any(option =>
                        string.Equals(option.Value, value, StringComparison.Ordinal)):
                    return Result.Failure(CreatorErrors.GrantFieldInvalid);

                case GrantFieldKind.Text:
                case GrantFieldKind.LongText:
                default:
                    break;
            }
        }

        foreach (var declaration in grant.Declarations)
        {
            if (!_acceptedDeclarations.Exists(accepted =>
                string.Equals(accepted.Key, declaration.Key, StringComparison.OrdinalIgnoreCase)))
            {
                return Result.Failure(CreatorErrors.GrantDeclarationRequired);
            }
        }

        if (grant.AllowsAmountRequest && RequestedMinorUnits is { } requested)
        {
            var minimum = grant.FundingMinimumMinorUnits ?? 1;
            var maximum = grant.FundingMaximumMinorUnits ?? grant.FundingMinorUnits;

            if (requested < minimum || requested > maximum)
            {
                return Result.Failure(CreatorErrors.GrantAmountOutOfRange);
            }
        }

        return Result.Success();
    }

    /// <summary>A reviewer picks the application up.</summary>
    public Result BeginReview(Guid reviewerUserId, DateTimeOffset now) =>
        Move(GrantApplicationState.UnderReview, reviewerUserId, null, now);

    /// <summary>A reviewer asks the applicant for something.</summary>
    public Result RequestInformation(
        Guid reviewerUserId,
        IEnumerable<string> requests,
        DateTimeOffset? respondBy,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(requests);

        return Move(
            GrantApplicationState.MoreInformationRequested, reviewerUserId, null, now, application =>
            {
                application._informationRequests.Clear();

                foreach (var request in requests
                    .Where(request => !string.IsNullOrWhiteSpace(request))
                    .Select(request => request.Trim())
                    .Take(CreatorLimits.MaxInformationRequests))
                {
                    application._informationRequests.Add(
                        GrantInformationRequest.Create(request, now));
                }

                application.RespondByAt = respondBy;
            });
    }

    /// <summary>
    /// Approved, for an amount the reviewer decided.
    /// <para>
    /// The amount is the reviewer's, bounded by the grant. An approval that took the applicant's
    /// requested figure on trust would be the client deciding an award, which §10 forbids in as many
    /// words.
    /// </para>
    /// </summary>
    public Result Approve(
        CommunityGrant grant,
        Guid reviewerUserId,
        long awardedMinorUnits,
        string? reason,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(grant);

        if (awardedMinorUnits <= 0)
        {
            return Result.Failure(CreatorErrors.GrantAmountOutOfRange);
        }

        var maximum = grant.FundingMaximumMinorUnits ?? grant.FundingMinorUnits;

        if (awardedMinorUnits > maximum)
        {
            return Result.Failure(CreatorErrors.GrantAmountOutOfRange);
        }

        return Move(GrantApplicationState.Approved, reviewerUserId, reason, now, application =>
        {
            application.AwardedMinorUnits = awardedMinorUnits;
            application.Currency = grant.Currency;
            application.DecidedAt = now;
            application.DecisionReason = Trim(reason);
            application.RespondByAt = null;
        });
    }

    public Result Reject(Guid reviewerUserId, string? reason, DateTimeOffset now) =>
        Move(GrantApplicationState.Rejected, reviewerUserId, reason, now, application =>
        {
            application.DecidedAt = now;
            application.DecisionReason = Trim(reason);
            application.RespondByAt = null;
        });

    /// <summary>The applicant withdraws. Their own act, and the only decision they may make.</summary>
    public Result Withdraw(DateTimeOffset now) =>
        Move(GrantApplicationState.Withdrawn, ApplicantUserId, null, now, application =>
        {
            application.DecidedAt = now;
        });

    /// <summary>
    /// Funding landed and the grant is running.
    /// <para>
    /// <see cref="FundingReference"/> is B15's own handle for the movement. Recording it here is
    /// what makes "approved but unfunded" visible rather than an assumption.
    /// </para>
    /// </summary>
    public Result Activate(string fundingReference, DateTimeOffset now) =>
        Move(GrantApplicationState.Active, actorUserId: null, null, now, application =>
        {
            application.FundingReference = Trim(fundingReference);
        });

    public Result Complete(Guid reviewerUserId, DateTimeOffset now) =>
        Move(GrantApplicationState.Completed, reviewerUserId, null, now);

    /// <summary>Adds a report the grant requires.</summary>
    public GrantReport AddReport(Guid reportId, string title, DateTimeOffset dueAt)
    {
        var report = GrantReport.Create(reportId, title, dueAt);

        _reports.Add(report);

        return report;
    }

    /// <summary>
    /// The applicant files a report against an active grant.
    /// <para>
    /// Refused unless the grant is active and the report is one this application is actually
    /// waiting for — §10: "Reports must be linked to the correct grant application."
    /// </para>
    /// </summary>
    public Result SubmitReport(Guid reportId, string? summary, DateTimeOffset now)
    {
        if (State != GrantApplicationState.Active)
        {
            return Result.Failure(CreatorErrors.GrantApplicationInvalidTransition);
        }

        var report = _reports.Find(candidate => candidate.ReportId == reportId);

        if (report is null)
        {
            return Result.Failure(CreatorErrors.GrantReportNotDue);
        }

        var filed = report.Submit(summary, now);

        if (filed.IsFailure)
        {
            return filed;
        }

        Touch(now);

        return Result.Success();
    }

    public bool CanWithdraw => GrantApplicationTransitions.CanWithdraw(State);

    public bool CanResubmit => State == GrantApplicationState.MoreInformationRequested;

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
        GrantApplicationState target,
        Guid? actorUserId,
        string? reason,
        DateTimeOffset now,
        Action<GrantApplication>? mutate = null)
    {
        if (!GrantApplicationTransitions.IsAllowed(State, target))
        {
            return Result.Failure(CreatorErrors.GrantApplicationInvalidTransition);
        }

        var previous = State;

        Record(previous, target, actorUserId, reason, now);

        State = target;
        mutate?.Invoke(this);
        Touch(now);

        // Only a decided outcome is announced, and the payload carries the amount rather than the
        // answers — see the class summary.
        if (target is GrantApplicationState.Approved
            or GrantApplicationState.Rejected
            or GrantApplicationState.Withdrawn
            or GrantApplicationState.Completed)
        {
            RaiseDomainEvent(new GrantApplicationDecidedDomainEvent(
                Id,
                GrantId,
                CreatorId,
                ApplicantUserId,
                previous,
                target,
                AwardedMinorUnits,
                Currency,
                Trim(reason),
                now));
        }

        return Result.Success();
    }

    private void Record(
        GrantApplicationState from,
        GrantApplicationState to,
        Guid? actorUserId,
        string? reason,
        DateTimeOffset now) =>
        _decisions.Add(GrantDecisionRecord.Create(
            Guid.NewGuid(), from, to, actorUserId, Trim(reason), now));

    private static string? Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private void Touch(DateTimeOffset now)
    {
        Revision++;
        ModifiedAt = now;
    }
}

/// <summary>One answer, against a field key the form defines.</summary>
public sealed class GrantAnswer : ValueObject
{
    private GrantAnswer(string fieldKey, string value)
    {
        FieldKey = fieldKey;
        Value = value;
    }

    private GrantAnswer() { }

    public string FieldKey { get; private set; } = string.Empty;

    public string Value { get; private set; } = string.Empty;

    public static GrantAnswer Create(string fieldKey, string value) => new(fieldKey.Trim(), value);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return FieldKey;
    }
}

/// <summary>Supporting material, by server-issued reference. Never file contents.</summary>
public sealed class GrantAttachment : ValueObject
{
    private GrantAttachment(string reference, DateTimeOffset attachedAt)
    {
        Reference = reference;
        AttachedAt = attachedAt;
    }

    private GrantAttachment() { }

    public string Reference { get; private set; } = string.Empty;

    public DateTimeOffset AttachedAt { get; private set; }

    public static GrantAttachment Create(string reference, DateTimeOffset attachedAt) =>
        new(reference, attachedAt);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Reference;
    }
}

/// <summary>A declaration the applicant accepted.</summary>
public sealed class GrantAcceptedDeclaration : ValueObject
{
    private GrantAcceptedDeclaration(string key) => Key = key;

    private GrantAcceptedDeclaration() { }

    public string Key { get; private set; } = string.Empty;

    public static GrantAcceptedDeclaration Create(string key) => new(key.Trim());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Key;
    }
}

/// <summary>Something a reviewer asked the applicant for.</summary>
public sealed class GrantInformationRequest : ValueObject
{
    private GrantInformationRequest(string text, DateTimeOffset requestedAt)
    {
        Text = text;
        RequestedAt = requestedAt;
    }

    private GrantInformationRequest() { }

    public string Text { get; private set; } = string.Empty;

    public DateTimeOffset RequestedAt { get; private set; }

    public static GrantInformationRequest Create(string text, DateTimeOffset requestedAt) =>
        new(text.Trim(), requestedAt);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Text;
        yield return RequestedAt;
    }
}

/// <summary>A report an active grant requires.</summary>
public sealed class GrantReport
{
    private GrantReport(Guid reportId, string title, DateTimeOffset dueAt)
    {
        ReportId = reportId;
        Title = title;
        DueAt = dueAt;
        State = GrantReportState.Pending;
    }

    private GrantReport() { }

    public Guid ReportId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public DateTimeOffset DueAt { get; private set; }

    public GrantReportState State { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }

    public string? Summary { get; private set; }

    public static GrantReport Create(Guid reportId, string title, DateTimeOffset dueAt) =>
        new(reportId, title.Trim(), dueAt);

    internal Result Submit(string? summary, DateTimeOffset now)
    {
        if (State is GrantReportState.Submitted or GrantReportState.Accepted)
        {
            return Result.Failure(CreatorErrors.GrantReportNotDue);
        }

        State = GrantReportState.Submitted;
        SubmittedAt = now;

        if (!string.IsNullOrWhiteSpace(summary))
        {
            Summary = summary.Trim()[..Math.Min(summary.Trim().Length, CreatorLimits.MaxAnswerLength)];
        }

        return Result.Success();
    }

    /// <summary>Whether this report is late. Computed, so an unswept row still reads correctly.</summary>
    public GrantReportState StateAt(DateTimeOffset now) =>
        State == GrantReportState.Pending && now > DueAt ? GrantReportState.Overdue : State;
}

/// <summary>One entry in the application's audit trail. §29 of the phase brief.</summary>
public sealed class GrantDecisionRecord : ValueObject
{
    private GrantDecisionRecord(
        Guid recordId,
        GrantApplicationState previousState,
        GrantApplicationState currentState,
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

    private GrantDecisionRecord() { }

    public Guid RecordId { get; private set; }

    public GrantApplicationState PreviousState { get; private set; }

    public GrantApplicationState CurrentState { get; private set; }

    public Guid? ActorUserId { get; private set; }

    public string? Reason { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    public static GrantDecisionRecord Create(
        Guid recordId,
        GrantApplicationState previousState,
        GrantApplicationState currentState,
        Guid? actorUserId,
        string? reason,
        DateTimeOffset occurredAt) =>
        new(recordId, previousState, currentState, actorUserId, reason, occurredAt);

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return RecordId;
    }
}
