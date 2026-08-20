using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Creator.Application.Mapping;
using Diaspora.Modules.Creator.Application.Services;
using Diaspora.Modules.Creator.Contracts.V1.Requests;
using Diaspora.Modules.Creator.Contracts.V1.Responses;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Errors;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Domain.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Creator.Application.Verification;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// CREATOR-GAP-002 — creator programme verification.
//
// Five member routes and one reviewer route. The register's central claim is the boundary: "Trust
// verifies people, not programmes." Identity verification is a *requirement* checked against
// ITrustService at submission and rendered as an unmet requirement pointing at Trust's own flow;
// nothing here stores a document, a number or an image.
//
// The state machine is CreatorVerificationTransitions and every move goes through it. The reviewer
// route needs creator:verification:review, which an applicant does not hold — and even a reviewer
// who holds it is refused their own application.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The programme's requirements, evaluated against real state.
/// <para>
/// <b>Every <c>isMet</c> here is the server's.</b> The register: "A client that decided '3 of 5
/// guides, so not met' would be re-implementing an eligibility rule it does not own." The identity
/// requirement is Trust's answer and routes to Trust's own flow; the rest are read from this
/// module's counters.
/// </para>
/// </summary>
public sealed class CreatorVerificationRequirements
{
    /// <summary>Bumped when a requirement below changes. Served on the application.</summary>
    public const string Version = "creator-verification/1";

    private const long RequiredPublications = 3;
    private const long RequiredFollowers = 10;

    private readonly CreatorContext _context;

    public CreatorVerificationRequirements(CreatorContext context) => _context = context;

    public async Task<IReadOnlyList<Domain.ValueObjects.CreatorRequirement>> EvaluateAsync(
        Domain.Creators.CreatorProfile creator, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creator);

        var identityVerified = await _context.IsIdentityVerifiedAsync(
            creator.UserId, cancellationToken);

        return
        [
            // Trust's answer, rendered as a requirement with a route into Trust's own flow. §8 of
            // the register: "the identity requirement is rendered as an unmet requirement pointing
            // at the real Trust flow, and only the programme wrapper is simulated."
            Domain.ValueObjects.CreatorRequirement.Flag(
                "identity-verified",
                "Verify your identity",
                identityVerified,
                "The creator programme requires a verified identity. This is a separate step, run by "
                + "the platform's Trust service.",
                "/verification/identity",
                "Verify identity"),

            Domain.ValueObjects.CreatorRequirement.Counted(
                "publications",
                "Publish three pieces of content",
                creator.TotalContentPublished,
                RequiredPublications,
                actionRoute: "/knowledge/create",
                actionLabel: "Write a guide"),

            Domain.ValueObjects.CreatorRequirement.Counted(
                "followers",
                "Reach ten followers",
                creator.Followers,
                RequiredFollowers),

            Domain.ValueObjects.CreatorRequirement.Flag(
                "good-standing",
                "Keep your account in good standing",
                !creator.IsRestricted && !creator.IsSuspended)
        ];
    }

    /// <summary>Whether the member may start or submit an application at all.</summary>
    public static bool AreMet(IReadOnlyList<Domain.ValueObjects.CreatorRequirement> requirements)
    {
        ArgumentNullException.ThrowIfNull(requirements);

        return requirements.All(requirement => requirement.IsMet);
    }
}

/// <summary>The caller's own creator-programme application.</summary>
[Authorize(Permission = CreatorPermissions.ProfileRead)]
public sealed record GetMyCreatorVerificationQuery : IQuery<CreatorVerificationResponse>;

public sealed class GetMyCreatorVerificationQueryHandler
    : IQueryHandler<GetMyCreatorVerificationQuery, CreatorVerificationResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorVerificationRepository _applications;
    private readonly CreatorVerificationRequirements _requirements;

    public GetMyCreatorVerificationQueryHandler(
        CreatorContext context,
        ICreatorVerificationRepository applications,
        CreatorVerificationRequirements requirements)
    {
        _context = context;
        _applications = applications;
        _requirements = requirements;
    }

    public async Task<Result<CreatorVerificationResponse>> Handle(
        GetMyCreatorVerificationQuery query, CancellationToken cancellationToken)
    {
        var caller = _context.RequireUser();

        if (caller.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(caller.Error);
        }

        // A member who has never done anything creator-shaped has no record, and the honest answer
        // is NotEligible with the requirements spelled out — not a 404. Refusing here would be the
        // "there is no creator aggregate" failure this whole phase exists to close, and opening a
        // record from a GET would be a side effect the route does not need.
        var persisted = await _context.FindCreatorAsync(cancellationToken);
        var creator = persisted ?? _context.TransientProfileFor(caller.Value);

        var requirements = await _requirements.EvaluateAsync(creator, cancellationToken);

        // Only a persisted creator can have an application. Looking one up against a transient
        // identifier would ask the database about a row that by construction cannot exist.
        var application = persisted is null
            ? null
            : await _applications.FindForCreatorAsync(persisted.Id, cancellationToken);

        // No application yet, so the state is the programme's opinion of the member rather than a
        // row: NotEligible until the requirements are met, then Eligible. That is the register's own
        // first two states and the reason they exist.
        var state = application?.State
            ?? (CreatorVerificationRequirements.AreMet(requirements)
                ? CreatorVerificationState.Eligible
                : CreatorVerificationState.NotEligible);

        return Result.Success(application.ToResponse(state, requirements));
    }
}

/// <summary>Starts a draft. Refused unless the programme's requirements are met.</summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.verification.start")]
public sealed record StartCreatorVerificationCommand : ICommand<CreatorVerificationResponse>;

public sealed class StartCreatorVerificationCommandHandler
    : ICommandHandler<StartCreatorVerificationCommand, CreatorVerificationResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorVerificationRepository _applications;
    private readonly CreatorVerificationRequirements _requirements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public StartCreatorVerificationCommandHandler(
        CreatorContext context,
        ICreatorVerificationRepository applications,
        CreatorVerificationRequirements requirements,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _requirements = requirements;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CreatorVerificationResponse>> Handle(
        StartCreatorVerificationCommand command, CancellationToken cancellationToken)
    {
        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(allowed.Error);
        }

        var requirements = await _requirements.EvaluateAsync(creator.Value, cancellationToken);

        var existing = await _applications.FindForCreatorAsync(creator.Value.Id, cancellationToken);

        if (existing is not null)
        {
            // A draft that already exists is the answer rather than a conflict — starting twice is
            // an ordinary double-tap, and a 409 here would make the screen look broken.
            if (CreatorVerificationTransitions.IsEditable(existing.State))
            {
                return Result.Success(existing.ToResponse(existing.State, requirements));
            }

            // An expired approval is a fresh start rather than a dead end.
            if (existing.State == CreatorVerificationState.Expired)
            {
                var reopened = existing.ReturnToDraft(_clock.UtcNow);

                if (reopened.IsFailure)
                {
                    return Result.Failure<CreatorVerificationResponse>(reopened.Error);
                }

                _applications.Update(existing);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                return Result.Success(existing.ToResponse(existing.State, requirements));
            }

            return Result.Failure<CreatorVerificationResponse>(
                CreatorErrors.VerificationInvalidTransition);
        }

        if (!CreatorVerificationRequirements.AreMet(requirements))
        {
            return Result.Failure<CreatorVerificationResponse>(
                CreatorErrors.VerificationNotEligible);
        }

        var application = CreatorVerificationApplication.Start(
            CreatorVerificationApplicationId.From(_guids.NewSequentialGuid()),
            creator.Value.Id,
            creator.Value.UserId,
            _clock.UtcNow);

        _applications.Add(application);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(application.ToResponse(application.State, requirements));
    }
}

/// <summary>
/// Attaches supporting material by reference.
/// <para>
/// The server issues the identifier and produces the label. What the client sends is a kind and a
/// hint; what is stored carries neither a file nor a path.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.Participate)]
public sealed record AttachCreatorEvidenceCommand : ICommand<CreatorEvidenceResponse>
{
    public required AttachCreatorEvidenceRequest Request { get; init; }
}

public sealed class AttachCreatorEvidenceCommandValidator
    : AbstractValidator<AttachCreatorEvidenceCommand>
{
    public AttachCreatorEvidenceCommandValidator()
    {
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Kind).NotEmpty().MaximumLength(32);
    }
}

public sealed class AttachCreatorEvidenceCommandHandler
    : ICommandHandler<AttachCreatorEvidenceCommand, CreatorEvidenceResponse>
{
    /// <summary>
    /// The kinds the programme accepts.
    /// <para>
    /// A closed set, because an open one would let a client name a kind that meant something to
    /// nobody and a reviewer could not act on.
    /// </para>
    /// </summary>
    private static readonly string[] AcceptedKinds =
        ["portfolio", "publication", "endorsement", "credential", "other"];

    private readonly CreatorContext _context;
    private readonly ICreatorVerificationRepository _applications;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;

    public AttachCreatorEvidenceCommandHandler(
        CreatorContext context,
        ICreatorVerificationRepository applications,
        IUnitOfWork unitOfWork,
        IGuidGenerator guids,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _unitOfWork = unitOfWork;
        _guids = guids;
        _clock = clock;
    }

    public async Task<Result<CreatorEvidenceResponse>> Handle(
        AttachCreatorEvidenceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var kind = command.Request.Kind.Trim().ToLowerInvariant();

        if (!AcceptedKinds.Contains(kind, StringComparer.Ordinal))
        {
            return Result.Failure<CreatorEvidenceResponse>(CreatorErrors.EvidenceKindUnknown);
        }

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorEvidenceResponse>(creator.Error);
        }

        var application = await _applications.FindForCreatorAsync(
            creator.Value.Id, cancellationToken);

        if (application is null)
        {
            return Result.Failure<CreatorEvidenceResponse>(CreatorErrors.VerificationNotFound);
        }

        // **The label is entirely the server's, and that is not a detail.** An earlier version took
        // a client hint and prefixed it; a client sending a local file path got that path stored and
        // served back to a reviewer. What a reviewer needs is what kind of thing this is and which
        // one it is — both of which the server already knows.
        var ordinal = application.Evidence.Count(
            existing => string.Equals(existing.Kind, kind, StringComparison.Ordinal)) + 1;

        var label = $"{char.ToUpperInvariant(kind[0])}{kind[1..]} {ordinal}";

        var attached = application.AttachEvidence(
            _guids.NewSequentialGuid(), kind, label, _clock.UtcNow);

        if (attached.IsFailure)
        {
            return Result.Failure<CreatorEvidenceResponse>(attached.Error);
        }

        _applications.Update(application);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(attached.Value.ToResponse());
    }
}

/// <summary>
/// Hands the application in. Idempotent on the key.
/// <para>
/// <b>The requirements are re-checked here, against Trust and against the counters.</b> What the
/// member acknowledged is recorded and not believed — §7 of the phase brief, and the reason the
/// acknowledgement list exists at all.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.verification.submit")]
public sealed record SubmitCreatorVerificationCommand : ICommand<CreatorVerificationResponse>
{
    public required SubmitCreatorVerificationRequest Request { get; init; }

    public string? IdempotencyKeyHeader { get; init; }
}

public sealed class SubmitCreatorVerificationCommandHandler
    : ICommandHandler<SubmitCreatorVerificationCommand, CreatorVerificationResponse>
{
    private const string Operation = "creator-verification-submit";

    private readonly CreatorContext _context;
    private readonly ICreatorVerificationRepository _applications;
    private readonly CreatorVerificationRequirements _requirements;
    private readonly CreatorStatusService _status;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SubmitCreatorVerificationCommandHandler(
        CreatorContext context,
        ICreatorVerificationRepository applications,
        CreatorVerificationRequirements requirements,
        CreatorStatusService status,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _requirements = requirements;
        _status = status;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreatorVerificationResponse>> Handle(
        SubmitCreatorVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(creator.Error);
        }

        var allowed = CreatorContext.EnsureCanParticipate(creator.Value);

        if (allowed.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(allowed.Error);
        }

        var requirements = await _requirements.EvaluateAsync(creator.Value, cancellationToken);

        var key = CreatorIdempotency.Normalise(
            Operation, command.IdempotencyKeyHeader, command.Request.IdempotencyKey);

        if (key is not null)
        {
            var replayed = await _applications.FindByIdempotencyKeyAsync(key, cancellationToken);

            if (replayed is not null)
            {
                return Result.Success(replayed.ToResponse(replayed.State, requirements));
            }
        }

        var application = await _applications.FindForCreatorAsync(
            creator.Value.Id, cancellationToken);

        if (application is null)
        {
            return Result.Failure<CreatorVerificationResponse>(CreatorErrors.VerificationNotFound);
        }

        // Already handed in. Answering with the current state rather than a 409 is what makes a
        // retried submit safe on a flaky connection.
        if (CreatorVerificationTransitions.IsWithReviewer(application.State))
        {
            return Result.Success(application.ToResponse(application.State, requirements));
        }

        if (!CreatorVerificationRequirements.AreMet(requirements))
        {
            return Result.Failure<CreatorVerificationResponse>(
                CreatorErrors.VerificationRequirementsUnmet);
        }

        var submitted = application.Submit(
            command.Request.AcknowledgedRequirements ?? [],
            command.Request.EvidenceIds ?? [],
            command.Request.Note,
            key,
            _clock.UtcNow);

        if (submitted.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(submitted.Error);
        }

        _applications.Update(application);

        // Submitting moves the member to PendingVerification, which is a served status the client
        // renders. Refreshing here rather than waiting for their next read means the event fires
        // when the fact happened.
        await _status.RefreshAsync(creator.Value, cancellationToken);

        var commit = await CreatorIdempotency.GuardAsync(
            key,
            token => _unitOfWork.SaveChangesAsync(token),
            token => _applications.FindByIdempotencyKeyAsync(key!, token),
            cancellationToken);

        if (commit.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(commit.Error);
        }

        var rendered = commit.Value.Replayed ?? application;

        return Result.Success(rendered.ToResponse(rendered.State, requirements));
    }
}

/// <summary>The member appeals a refusal. Once.</summary>
[Authorize(Permission = CreatorPermissions.Participate)]
[Auditable(Action = "creator.verification.appeal")]
public sealed record AppealCreatorVerificationCommand : ICommand<CreatorVerificationResponse>
{
    public required AppealCreatorVerificationRequest Request { get; init; }
}

public sealed class AppealCreatorVerificationCommandHandler
    : ICommandHandler<AppealCreatorVerificationCommand, CreatorVerificationResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorVerificationRepository _applications;
    private readonly CreatorVerificationRequirements _requirements;
    private readonly CreatorStatusService _status;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AppealCreatorVerificationCommandHandler(
        CreatorContext context,
        ICreatorVerificationRepository applications,
        CreatorVerificationRequirements requirements,
        CreatorStatusService status,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _requirements = requirements;
        _status = status;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreatorVerificationResponse>> Handle(
        AppealCreatorVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var creator = await _context.RequireCreatorAsync(cancellationToken);

        if (creator.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(creator.Error);
        }

        var application = await _applications.FindForCreatorAsync(
            creator.Value.Id, cancellationToken);

        if (application is null)
        {
            return Result.Failure<CreatorVerificationResponse>(CreatorErrors.VerificationNotFound);
        }

        if (!application.CanAppeal)
        {
            return Result.Failure<CreatorVerificationResponse>(
                CreatorErrors.VerificationCannotAppeal);
        }

        var appealed = application.Appeal(command.Request?.Reason, _clock.UtcNow);

        if (appealed.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(appealed.Error);
        }

        _applications.Update(application);

        await _status.RefreshAsync(creator.Value, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requirements = await _requirements.EvaluateAsync(creator.Value, cancellationToken);

        return Result.Success(application.ToResponse(application.State, requirements));
    }
}

/// <summary>
/// A reviewer's decision.
/// <para>
/// <b>Needs <c>creator:verification:review</c>, and refuses the reviewer's own application.</b> §30
/// of the phase brief: "Creator verification decisions must not be member-controlled. Separate
/// applicant, reviewer, administrator." Holding the permission and being the applicant are both true
/// for a member of staff who applied, so the two checks are separate.
/// </para>
/// </summary>
[Authorize(Permission = CreatorPermissions.VerificationReview)]
[Transactional(Reason =
    "Writes the decision and the creator's status in one unit. A decision that committed without "
    + "the status would leave a verified creator the platform still calls Emerging.")]
[Auditable(Action = "creator.verification.decide")]
public sealed record DecideCreatorVerificationCommand : ICommand<CreatorVerificationResponse>
{
    public required Guid ApplicationId { get; init; }

    public required DecideCreatorVerificationRequest Request { get; init; }
}

public sealed class DecideCreatorVerificationCommandValidator
    : AbstractValidator<DecideCreatorVerificationCommand>
{
    public DecideCreatorVerificationCommandValidator()
    {
        RuleFor(command => command.ApplicationId).NotEmpty();
        RuleFor(command => command.Request).NotNull();
        RuleFor(command => command.Request.Action).NotEmpty();
        RuleFor(command => command.Request.Reason).MaximumLength(4000);
    }
}

public sealed class DecideCreatorVerificationCommandHandler
    : ICommandHandler<DecideCreatorVerificationCommand, CreatorVerificationResponse>
{
    private readonly CreatorContext _context;
    private readonly ICreatorVerificationRepository _applications;
    private readonly ICreatorProfileRepository _creators;
    private readonly CreatorVerificationRequirements _requirements;
    private readonly CreatorStatusService _status;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public DecideCreatorVerificationCommandHandler(
        CreatorContext context,
        ICreatorVerificationRepository applications,
        ICreatorProfileRepository creators,
        CreatorVerificationRequirements requirements,
        CreatorStatusService status,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _context = context;
        _applications = applications;
        _creators = creators;
        _requirements = requirements;
        _status = status;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<CreatorVerificationResponse>> Handle(
        DecideCreatorVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var reviewer = _context.RequireUser();

        if (reviewer.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(reviewer.Error);
        }

        var application = await _applications.GetAsync(
            CreatorVerificationApplicationId.From(command.ApplicationId), cancellationToken);

        if (application is null)
        {
            return Result.Failure<CreatorVerificationResponse>(CreatorErrors.VerificationNotFound);
        }

        if (application.UserId == reviewer.Value)
        {
            return Result.Failure<CreatorVerificationResponse>(
                CreatorErrors.ReviewerCannotDecideOwn);
        }

        var now = _clock.UtcNow;

        var moved = command.Request.Action.Trim().ToLowerInvariant() switch
        {
            "review" => application.BeginReview(reviewer.Value, now),
            "more-information" => application.RequestInformation(
                reviewer.Value,
                command.Request.InformationRequested ?? [],
                command.Request.RespondBy,
                now),
            "approve" => application.Approve(reviewer.Value, command.Request.Reason, now),
            "reject" => application.Reject(reviewer.Value, command.Request.Reason, now),
            "suspend" => application.Suspend(reviewer.Value, command.Request.Reason, now),
            _ => Result.Failure(CreatorErrors.VerificationInvalidTransition)
        };

        if (moved.IsFailure)
        {
            return Result.Failure<CreatorVerificationResponse>(moved.Error);
        }

        _applications.Update(application);

        var creator = await _creators.GetAsync(application.CreatorId, cancellationToken);

        if (creator is null)
        {
            return Result.Failure<CreatorVerificationResponse>(CreatorErrors.CreatorNotFound);
        }

        // Saved before the status refresh so the refresh reads the decision it is reacting to. The
        // command is [Transactional], so the two saves are one unit.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _status.RefreshAsync(creator, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var requirements = await _requirements.EvaluateAsync(creator, cancellationToken);

        return Result.Success(application.ToResponse(application.State, requirements));
    }
}
