using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Extensibility;
using Diaspora.Modules.Trust.Observability;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using FluentValidation;

namespace Diaspora.Modules.Trust.Application.Verifications;

/// <summary>What a reviewer decided.</summary>
public enum ReviewDecision
{
    Approve = 0,
    PartiallyApprove = 1,
    Reject = 2,
    NeedMoreInfo = 3,
    Escalate = 4,
    Revoke = 5
}

/// <summary>
/// Records a reviewer's decision on a verification.
/// <para>
/// The single most consequential command in the module: an approval here is what eventually
/// moves a member's access state, awards their badges, and unlocks publishing. All of that
/// happens through the aggregate and the badge rules rather than in this handler, which does
/// the orchestration and nothing else.
/// </para>
/// <para>
/// The self-review rule is enforced in the aggregate, not here — a second entry point that
/// forgot to check would otherwise be one refactor away.
/// </para>
/// </summary>
[RequirePermission(TrustPermissions.ReviewVerification)]
[Transactional]
public sealed record ReviewVerificationCommand : ICommand<VerificationResponse>
{
    public required Guid VerificationId { get; init; }

    public required ReviewDecision Decision { get; init; }

    public VerificationRejectionReason? RejectionReason { get; init; }

    /// <summary>Shown to the member.</summary>
    public string? Details { get; init; }

    /// <summary>Internal notes. Never shown to the member.</summary>
    public string? Notes { get; init; }
}

public sealed class ReviewVerificationCommandValidator : AbstractValidator<ReviewVerificationCommand>
{
    public ReviewVerificationCommandValidator()
    {
        RuleFor(command => command.VerificationId).NotEmpty();

        RuleFor(command => command.Decision).IsInEnum();

        RuleFor(command => command.RejectionReason)
            .NotNull()
            .When(command => command.Decision == ReviewDecision.Reject)
            .WithMessage("A machine-readable rejection reason is required when rejecting.");

        RuleFor(command => command.Details)
            .NotEmpty()
            .When(command => command.Decision is ReviewDecision.Reject
                or ReviewDecision.NeedMoreInfo or ReviewDecision.Revoke)
            .WithMessage("An explanation the member can act on is required.");

        RuleFor(command => command.Details)
            .MaximumLength(VerificationRequest.MaxRejectionDetailsLength);

        RuleFor(command => command.Notes)
            .MaximumLength(VerificationReview.MaxNotesLength);
    }
}

public sealed class ReviewVerificationCommandHandler
    : ICommandHandler<ReviewVerificationCommand, VerificationResponse>
{
    private readonly IVerificationRequestRepository _verifications;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly ITrustEventRepository _timeline;
    private readonly VerificationTypeHandlerRegistry _handlers;
    private readonly ReputationService _reputation;
    private readonly BadgeEvaluator _badges;
    private readonly VerificationGrantApplier _grants;
    private readonly ICurrentUser _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ReviewVerificationCommandHandler(
        IVerificationRequestRepository verifications,
        TrustProfileProvisioner provisioner,
        ITrustEventRepository timeline,
        VerificationTypeHandlerRegistry handlers,
        ReputationService reputation,
        BadgeEvaluator badges,
        VerificationGrantApplier grants,
        ICurrentUser currentUser,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _verifications = verifications;
        _provisioner = provisioner;
        _timeline = timeline;
        _handlers = handlers;
        _reputation = reputation;
        _badges = badges;
        _grants = grants;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<VerificationResponse>> Handle(
        ReviewVerificationCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var reviewerId = _currentUser.UserId;
        if (reviewerId is null)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.NotAuthenticated);
        }

        var verification = await _verifications.GetAsync(
            VerificationRequestId.From(request.VerificationId), cancellationToken);

        if (verification is null)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.VerificationNotFound);
        }

        if (verification.UserId == reviewerId)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.SelfReviewForbidden);
        }

        var now = _clock.UtcNow;
        var reviewerName = _currentUser.Claims.TryGetValue("name", out var name) ? name : null;
        var handler = _handlers.For(verification.Type);

        try
        {
            switch (request.Decision)
            {
                case ReviewDecision.Approve:
                case ReviewDecision.PartiallyApprove:
                    verification.Approve(
                        reviewerId,
                        reviewerName,
                        request.Notes,
                        now,
                        handler?.GrantLifetime is { } lifetime ? now.Add(lifetime) : null,
                        partial: request.Decision == ReviewDecision.PartiallyApprove);
                    break;

                case ReviewDecision.Reject:
                    verification.Reject(
                        reviewerId,
                        reviewerName,
                        request.RejectionReason ?? VerificationRejectionReason.INVALID_DOCUMENT,
                        request.Details,
                        now);
                    break;

                case ReviewDecision.NeedMoreInfo:
                    verification.RequestMoreInformation(
                        reviewerId, reviewerName, request.Details!, now);
                    break;

                case ReviewDecision.Escalate:
                    verification.Escalate(reviewerId, reviewerName, request.Notes, now);
                    break;

                case ReviewDecision.Revoke:
                    verification.Revoke(reviewerId, reviewerName, request.Details!, now);
                    break;
            }
        }
        catch (DomainException exception)
        {
            // An illegal transition is a conflict, not a server fault — the reviewer's view
            // was stale and someone else has already decided this.
            return Result.Failure<VerificationResponse>(
                Error.Conflict(TrustErrorCodes.VerificationNotPending, exception.Message));
        }

        await ApplyOutcomeAsync(verification, request.Decision, now, cancellationToken);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        RecordTelemetry(verification, request.Decision, now);

        return Result.Success(verification.ToResponse());
    }

    /// <summary>
    /// Propagates the decision to the member's standing.
    /// <para>
    /// Grants and losses both run through <see cref="TrustProfile"/>, then badges are
    /// reconciled against the rules. Reconciling rather than awarding directly is what makes
    /// a revocation take the badge back without anyone having written code to do so.
    /// </para>
    /// </summary>
    private async Task ApplyOutcomeAsync(
        VerificationRequest verification,
        ReviewDecision decision,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var (profile, reputation) = await _provisioner.GetOrCreateBothAsync(
            verification.UserId, cancellationToken);

        var country = verification.TargetCountry ?? verification.ResidenceCountry;

        switch (decision)
        {
            case ReviewDecision.Approve:
            case ReviewDecision.PartiallyApprove:
            {
                // Shared with the automatic path, which used to do none of this. See
                // VerificationGrantApplier.
                await _grants.ApplyApprovalAsync(
                    verification,
                    "Your verification was approved by the compliance team.",
                    now,
                    cancellationToken);

                break;
            }

            case ReviewDecision.Reject:
            {
                await _timeline.AddAsync(
                    TrustEvent.Record(
                        verification.UserId,
                        TrustEventTypes.VerificationRejected,
                        $"{Humanize(verification.Type)} verification declined",
                        now,
                        verification.RejectionDetails,
                        TrustEventTypes.IconFor(TrustEventTypes.VerificationRejected),
                        country),
                    cancellationToken);

                break;
            }

            case ReviewDecision.Revoke:
            {
                profile.RecordGrantLost(
                    verification.Type, country, verification.RejectionDetails ?? "Revoked.", now);

                await _timeline.AddAsync(
                    TrustEvent.Record(
                        verification.UserId,
                        TrustEventTypes.VerificationRevoked,
                        $"{Humanize(verification.Type)} verification withdrawn",
                        now,
                        verification.RejectionDetails,
                        TrustEventTypes.IconFor(TrustEventTypes.VerificationRevoked),
                        country),
                    cancellationToken);

                break;
            }
        }

        // Badges follow standing, in both directions.
        _badges.Reconcile(profile, reputation, profile.HasBadge(BadgeType.Moderator), now);
    }

    private static void RecordTelemetry(
        VerificationRequest verification, ReviewDecision decision, DateTimeOffset now)
    {
        TrustTelemetrySource.VerificationsDecided.Add(
            1,
            new KeyValuePair<string, object?>("type", verification.Type.ToString()),
            new KeyValuePair<string, object?>("decision", decision.ToString()),
            new KeyValuePair<string, object?>("automated", false));

        TrustTelemetrySource.ReviewLatencySeconds.Record(
            (now - verification.SubmittedAt).TotalSeconds,
            new KeyValuePair<string, object?>("type", verification.Type.ToString()));
    }

    private static string Humanize(VerificationType type) =>
        System.Text.RegularExpressions.Regex.Replace(type.ToString(), "(?<!^)([A-Z])", " $1");
}
