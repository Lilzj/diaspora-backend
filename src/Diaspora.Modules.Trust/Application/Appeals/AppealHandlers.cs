using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Trust.Application.Appeals;

/// <summary>
/// Files an appeal against a trust decision.
/// <para>
/// One command for all four appeal types, which is what lets a single
/// <c>POST /api/v1/trust/appeals</c> replace the two endpoints the frontend mocks — its
/// generic appeals form and its diaspora-specific appeal both land here.
/// </para>
/// </summary>
[RequirePermission(TrustPermissions.SubmitAppeal)]
[Transactional]
public sealed record SubmitAppealCommand : ICommand<AppealResponse>
{
    public required AppealType Type { get; init; }

    public required string TargetId { get; init; }

    public required string Reason { get; init; }

    public IReadOnlyList<string>? EvidenceKeys { get; init; }

    public bool IsCorrectionRequest { get; init; }
}

public sealed class SubmitAppealCommandValidator : AbstractValidator<SubmitAppealCommand>
{
    public SubmitAppealCommandValidator()
    {
        RuleFor(command => command.Type).IsInEnum();

        RuleFor(command => command.TargetId)
            .NotEmpty()
            .MaximumLength(Appeal.MaxTargetIdLength);

        RuleFor(command => command.Reason)
            .NotEmpty()
            .MinimumLength(20)
            .WithMessage("Please explain your appeal in at least 20 characters.")
            .MaximumLength(Appeal.MaxReasonLength);

        RuleFor(command => command.EvidenceKeys)
            .Must(keys => keys is null || keys.Count <= Appeal.MaxEvidenceItems)
            .WithMessage($"At most {Appeal.MaxEvidenceItems} evidence items.");
    }
}

public sealed class SubmitAppealCommandHandler : ICommandHandler<SubmitAppealCommand, AppealResponse>
{
    private readonly IAppealRepository _appeals;
    private readonly IVerificationRequestRepository _verifications;
    private readonly IModerationItemRepository _cases;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly ITrustEventRepository _timeline;
    private readonly IAccountSuspensionRecord _suspensions;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public SubmitAppealCommandHandler(
        IAppealRepository appeals,
        IVerificationRequestRepository verifications,
        IModerationItemRepository cases,
        TrustProfileProvisioner provisioner,
        ITrustEventRepository timeline,
        IAccountSuspensionRecord suspensions,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _appeals = appeals;
        _verifications = verifications;
        _cases = cases;
        _provisioner = provisioner;
        _timeline = timeline;
        _suspensions = suspensions;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AppealResponse>> Handle(
        SubmitAppealCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<AppealResponse>(TrustErrors.NotAuthenticated);
        }

        // One open appeal per target. Otherwise a member can file the same appeal ten times
        // and ten reviewers work it.
        var open = await _appeals.GetOpenForTargetAsync(userId, request.TargetId, cancellationToken);
        if (open is not null)
        {
            return Result.Failure<AppealResponse>(TrustErrors.AppealAlreadyOpen);
        }

        // A points hold is the appellant's own (CS-4a). Trust cannot see the hold, so it does not
        // check one exists; it does refuse an appeal of anybody else's.
        if (request.Type == AppealType.RewardHold
            && (!Guid.TryParse(request.TargetId, out var heldMember) || heldMember != userId.Value))
        {
            return Result.Failure<AppealResponse>(TrustErrors.AppealNotAllowed);
        }

        var now = _clock.UtcNow;

        VerificationRequest? verification = null;
        UserId? originalDecider = null;
        DateTimeOffset? decidedAt = null;

        // A verification appeal is checked against the actual decision: it must exist, belong
        // to the appellant, and be in a state that can be appealed at all.
        if (request.Type == AppealType.Verification && Guid.TryParse(request.TargetId, out var id))
        {
            verification = await _verifications.GetAsync(
                VerificationRequestId.From(id), cancellationToken);

            if (verification is null || verification.UserId != userId)
            {
                return Result.Failure<AppealResponse>(TrustErrors.VerificationNotFound);
            }

            if (!VerificationWorkflow.IsAppealable(verification.Status))
            {
                return Result.Failure<AppealResponse>(TrustErrors.AppealNotAllowed);
            }

            decidedAt = verification.ReviewedAt;

            // Recorded so the appeal reviewer cannot be the person being appealed against.
            originalDecider = verification.Reviews
                .OrderByDescending(review => review.DecidedAt)
                .Select(review => review.ReviewerId)
                .FirstOrDefault(reviewer => reviewer is not null);
        }

        // A content appeal is against a moderation decision on the member's own content (7 Nov 2026).
        // Until now its target was free text that nothing checked, the moderator who decided could
        // review the appeal, and an upheld appeal restored nothing. Now the target is the case: it
        // must be the appellant's, something must have been taken down, the window (14 days) must be
        // open, and neither member of staff behind the decision may review it.
        ModerationItem? moderationCase = null;
        UserId? secondDecider = null;

        if (request.Type == AppealType.Content)
        {
            if (!Guid.TryParse(request.TargetId, out var caseId))
            {
                return Result.Failure<AppealResponse>(TrustErrors.AppealNotAllowed);
            }

            moderationCase = await _cases.GetAsync(ModerationItemId.From(caseId), cancellationToken);

            if (moderationCase is null || moderationCase.AuthorId != userId)
            {
                return Result.Failure<AppealResponse>(TrustErrors.QueueItemNotFound);
            }

            if (!moderationCase.AppealableBy(userId, now))
            {
                return Result.Failure<AppealResponse>(TrustErrors.AppealWindowClosed);
            }

            originalDecider = moderationCase.FirstDeciderId
                ?? moderationCase.Decisions
                    .Where(decision => decision.ModeratorId is not null)
                    .OrderByDescending(decision => decision.DecidedAt)
                    .Select(decision => decision.ModeratorId)
                    .FirstOrDefault();

            secondDecider = moderationCase.Decisions
                .Where(decision => decision.Kind == ModerationDecisionKind.Confirmation)
                .OrderByDescending(decision => decision.DecidedAt)
                .Select(decision => decision.ModeratorId)
                .FirstOrDefault();
        }

        // A suspension appeal is the appellant's own account, and only while it is suspended
        // (7 Nov 2026). Until now any member could appeal any account's "suspension", and the
        // members of staff who suspended them could review it. An upheld appeal reinstates the
        // member (Identity consumes the approval).
        if (request.Type == AppealType.Suspension)
        {
            if (!Guid.TryParse(request.TargetId, out var suspendedMember) || suspendedMember != userId.Value)
            {
                return Result.Failure<AppealResponse>(TrustErrors.AppealNotAllowed);
            }

            var suspension = await _suspensions.GetCurrentAsync(userId.Value, cancellationToken);
            if (suspension is null)
            {
                return Result.Failure<AppealResponse>(TrustErrors.AppealNotAllowed);
            }

            originalDecider = suspension.ConfirmedBy is { } confirmer ? UserId.From(confirmer) : null;
            secondDecider = suspension.ProposedBy is { } proposer ? UserId.From(proposer) : null;
        }

        Appeal appeal;
        try
        {
            appeal = Appeal.File(
                userId,
                request.Type,
                request.TargetId,
                request.Reason,
                now,
                decidedAt,
                reviewSla: TimeSpan.FromDays(7));
        }
        catch (DomainException exception) when (exception.Code == "trust.appeal.window-closed")
        {
            return Result.Failure<AppealResponse>(TrustErrors.AppealWindowClosed);
        }

        appeal.WithOriginalDecider(originalDecider)
            .WithSecondDecider(secondDecider)
            .AsCorrectionRequest(request.IsCorrectionRequest);

        foreach (var key in request.EvidenceKeys ?? [])
        {
            appeal.AttachEvidence(key);
        }

        await _appeals.AddAsync(appeal, cancellationToken);

        // The verification moves to Appealed so it cannot be decided again underneath the
        // appeal, and so the member's own view shows the appeal in progress.
        verification?.MarkAppealed(now);

        // The case waits for the appeal: its consequences cannot land while it is open.
        moderationCase?.MarkAppealed(now);

        var profile = await _provisioner.GetOrCreateAsync(userId, cancellationToken);
        profile.AppealOpened(now);

        await _timeline.AddAsync(
            TrustEvent.Record(
                userId,
                TrustEventTypes.AppealFiled,
                $"{request.Type} appeal filed",
                now,
                "Your appeal has been queued for review.",
                TrustEventTypes.IconFor(TrustEventTypes.AppealFiled)),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        TrustTelemetrySource.AppealsSubmitted.Add(
            1, new KeyValuePair<string, object?>("type", request.Type.ToString()));

        return Result.Success(appeal.ToResponse());
    }
}

/// <summary>
/// Decides an appeal.
/// <para>
/// Both fairness rules — not your own appeal, not an appeal against your own decision — are
/// enforced inside the aggregate. This handler translates the resulting
/// <see cref="DomainException"/> into a 403 rather than duplicating the checks, so there is
/// one definition of what makes an appeal reviewer eligible.
/// </para>
/// </summary>
[RequirePermission(TrustPermissions.ReviewAppeal)]
[Transactional]
public sealed record ReviewAppealCommand : ICommand<AppealResponse>
{
    public required Guid AppealId { get; init; }

    /// <summary>True upholds the appeal.</summary>
    public required bool Upheld { get; init; }

    public required string Explanation { get; init; }

    /// <summary>Escalate instead of deciding.</summary>
    public bool Escalate { get; init; }
}

public sealed class ReviewAppealCommandValidator : AbstractValidator<ReviewAppealCommand>
{
    public ReviewAppealCommandValidator()
    {
        RuleFor(command => command.AppealId).NotEmpty();

        RuleFor(command => command.Explanation)
            .NotEmpty()
            .WithMessage("The member must be told why.")
            .MaximumLength(Appeal.MaxExplanationLength);
    }
}

public sealed class ReviewAppealCommandHandler : ICommandHandler<ReviewAppealCommand, AppealResponse>
{
    private readonly IAppealRepository _appeals;
    private readonly IVerificationRequestRepository _verifications;
    private readonly IModerationItemRepository _cases;
    private readonly Moderation.ModerationTargetRegistry _targets;
    private readonly Moderation.ModerationConsequences _consequences;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly ITrustEventRepository _timeline;
    private readonly BadgeEvaluator _badges;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ReviewAppealCommandHandler(
        IAppealRepository appeals,
        IVerificationRequestRepository verifications,
        IModerationItemRepository cases,
        Moderation.ModerationTargetRegistry targets,
        Moderation.ModerationConsequences consequences,
        TrustProfileProvisioner provisioner,
        ITrustEventRepository timeline,
        BadgeEvaluator badges,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _appeals = appeals;
        _verifications = verifications;
        _cases = cases;
        _targets = targets;
        _consequences = consequences;
        _provisioner = provisioner;
        _timeline = timeline;
        _badges = badges;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<AppealResponse>> Handle(
        ReviewAppealCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reviewerId = _currentUser.UserId;
        if (reviewerId is null)
        {
            return Result.Failure<AppealResponse>(TrustErrors.NotAuthenticated);
        }

        var appeal = await _appeals.GetAsync(AppealId.From(request.AppealId), cancellationToken);
        if (appeal is null)
        {
            return Result.Failure<AppealResponse>(TrustErrors.AppealNotFound);
        }

        var now = _clock.UtcNow;

        try
        {
            if (request.Escalate)
            {
                appeal.Escalate(reviewerId, request.Explanation, now);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                return Result.Success(appeal.ToResponse());
            }

            appeal.Decide(reviewerId, request.Upheld, request.Explanation, now);
        }
        catch (DomainException exception) when (exception.Code == "trust.review.self-forbidden")
        {
            return Result.Failure<AppealResponse>(TrustErrors.SelfReviewForbidden);
        }
        catch (DomainException exception)
        {
            return Result.Failure<AppealResponse>(
                Error.Conflict(TrustErrorCodes.AppealNotAllowed, exception.Message));
        }

        var (profile, reputation) = await _provisioner.GetOrCreateBothAsync(
            appeal.UserId, cancellationToken);

        profile.AppealClosed(now);

        // A verification appeal writes its outcome back to the verification, which is what
        // makes an upheld appeal actually grant the thing that was refused.
        if (appeal.Type == AppealType.Verification && Guid.TryParse(appeal.TargetId, out var targetId))
        {
            var verification = await _verifications.GetAsync(
                VerificationRequestId.From(targetId), cancellationToken);

            if (verification is not null && verification.Status == VerificationStatus.Appealed)
            {
                verification.ResolveAppeal(request.Upheld, request.Explanation, now);

                if (request.Upheld)
                {
                    profile.RecordGrant(
                        verification.Type,
                        verification.TargetCountry ?? verification.ResidenceCountry,
                        now);
                }
            }
        }

        // A content appeal writes its outcome back to the case (7 Nov 2026). Upheld, the content
        // comes back through its module and the owner is told; refused, a removal stands at once and
        // its consequences land now rather than waiting for the window.
        if (appeal.Type == AppealType.Content && Guid.TryParse(appeal.TargetId, out var caseId))
        {
            var moderationCase = await _cases.GetAsync(ModerationItemId.From(caseId), cancellationToken);

            if (moderationCase is not null && moderationCase.Status == ModerationQueueStatus.Appealed)
            {
                if (request.Upheld)
                {
                    moderationCase.UpholdAppeal(reviewerId, request.Explanation, now);

                    if (_targets.Find(moderationCase.TargetType) is { } target
                        && Guid.TryParse(moderationCase.ContentId, out var contentId))
                    {
                        var restored = await target.ApplyAsync(
                            contentId,
                            Diaspora.Contracts.Moderation.V1.ModerationOutcome.Restore,
                            new Diaspora.Contracts.Moderation.V1.ModerationActor(
                                reviewerId.Value,
                                moderationCase.Id.Value,
                                moderationCase.ReasonCode ?? Diaspora.Contracts.Moderation.V1.ModerationReasonCode.Other,
                                request.Explanation),
                            cancellationToken);

                        if (restored.IsFailure)
                        {
                            return Result.Failure<AppealResponse>(restored.Error);
                        }
                    }
                }
                else
                {
                    moderationCase.RefuseAppeal(now);
                    await _consequences.ApplyAsync(moderationCase, afterAppeal: true, now, cancellationToken);
                }
            }
        }

        _badges.Reconcile(profile, reputation, profile.HasBadge(BadgeType.Moderator), now);

        await _timeline.AddAsync(
            TrustEvent.Record(
                appeal.UserId,
                TrustEventTypes.AppealResolved,
                request.Upheld ? "Appeal upheld" : "Appeal declined",
                now,
                request.Explanation,
                TrustEventTypes.IconFor(TrustEventTypes.AppealResolved)),
            cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        TrustTelemetrySource.AppealsDecided.Add(
            1,
            new KeyValuePair<string, object?>("type", appeal.Type.ToString()),
            new KeyValuePair<string, object?>("upheld", request.Upheld));

        return Result.Success(appeal.ToResponse());
    }
}

/// <summary>The caller's own appeals.</summary>
[RequirePermission(TrustPermissions.ReadSelf)]
public sealed record ListMyAppealsQuery : IQuery<PagedResult<AppealResponse>>
{
    public PaginationRequest Pagination { get; init; } = new();

    public AppealStatus? Status { get; init; }
}

public sealed class ListMyAppealsQueryHandler
    : IQueryHandler<ListMyAppealsQuery, PagedResult<AppealResponse>>
{
    private readonly IAppealRepository _appeals;
    private readonly ICurrentUser _currentUser;

    public ListMyAppealsQueryHandler(IAppealRepository appeals, ICurrentUser currentUser)
    {
        _appeals = appeals;
        _currentUser = currentUser;
    }

    public async Task<Result<PagedResult<AppealResponse>>> Handle(
        ListMyAppealsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<PagedResult<AppealResponse>>(TrustErrors.NotAuthenticated);
        }

        var page = await _appeals.ListForUserAsync(
            userId, request.Pagination.Sanitize(), request.Status, cancellationToken);

        return Result.Success(page.Map(appeal => appeal.ToResponse()));
    }
}

/// <summary>One appeal with its history.</summary>
[RequirePermission(TrustPermissions.ReadSelf)]
public sealed record GetAppealQuery : IQuery<AppealDetailResponse>
{
    public required Guid AppealId { get; init; }
}

public sealed class GetAppealQueryHandler : IQueryHandler<GetAppealQuery, AppealDetailResponse>
{
    private readonly IAppealRepository _appeals;
    private readonly ICurrentUser _currentUser;

    public GetAppealQueryHandler(IAppealRepository appeals, ICurrentUser currentUser)
    {
        _appeals = appeals;
        _currentUser = currentUser;
    }

    public async Task<Result<AppealDetailResponse>> Handle(
        GetAppealQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var userId = _currentUser.UserId;
        if (userId is null)
        {
            return Result.Failure<AppealDetailResponse>(TrustErrors.NotAuthenticated);
        }

        var appeal = await _appeals.GetAsync(AppealId.From(request.AppealId), cancellationToken);
        if (appeal is null)
        {
            return Result.Failure<AppealDetailResponse>(TrustErrors.AppealNotFound);
        }

        var isStaff = _currentUser.Permissions.Contains(TrustPermissions.ReviewAppeal);

        if (appeal.UserId != userId && !isStaff)
        {
            return Result.Failure<AppealDetailResponse>(TrustErrors.AppealNotFound);
        }

        return Result.Success(appeal.ToDetailResponse(forStaff: isStaff));
    }
}

/// <summary>The staff appeals queue.</summary>
[RequirePermission(TrustPermissions.ReviewAppeal)]
public sealed record ListAppealQueueQuery : IQuery<PagedResult<AppealResponse>>
{
    public PaginationRequest Pagination { get; init; } = new();

    public AppealStatus? Status { get; init; }

    public AppealType? Type { get; init; }

    public Guid? AssignedTo { get; init; }
}

public sealed class ListAppealQueueQueryHandler
    : IQueryHandler<ListAppealQueueQuery, PagedResult<AppealResponse>>
{
    private readonly IAppealRepository _appeals;

    public ListAppealQueueQueryHandler(IAppealRepository appeals)
    {
        _appeals = appeals;
    }

    public async Task<Result<PagedResult<AppealResponse>>> Handle(
        ListAppealQueueQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var page = await _appeals.ListQueueAsync(
            request.Pagination.Sanitize(),
            request.Status,
            request.Type,
            request.AssignedTo is { } assignee ? UserId.From(assignee) : null,
            cancellationToken);

        return Result.Success(page.Map(appeal => appeal.ToResponse()));
    }
}
