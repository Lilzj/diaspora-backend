using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Trust.Application.Mapping;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Contracts.V1.Responses;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Modules.Trust.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Trust.Application.Verifications;

/// <summary>
/// Applies the outcome of a verification that was approved and never granted anything.
/// <para>
/// <b>An approval is two writes, and only one of them is the status.</b> Marking a request
/// <c>Approved</c> is what a reviewer sees; recording the grant, paying the handler's award,
/// reconciling the badges and writing the member's journey is what the approval is <em>for</em>.
/// <c>ReviewVerification</c> does both. Anything that sets the status by another route does the
/// first and not the second, and the member is left holding an approved verification that
/// granted them nothing.
/// </para>
/// <para>
/// That is not hypothetical. The platform's first Admin was bootstrapped by hand in SQL — there
/// was no staff account to approve anything, and no route to make one — so their diaspora
/// verification reads <c>Approved</c> with an empty grant set, leaving them on the
/// <c>Basic</c> tier. <c>ViewerContextResolver</c> reads that tier, so Community treated the
/// platform's only Admin as an unverified member.
/// </para>
/// <para>
/// <b>It decides nothing.</b> A request that is not already <c>Approved</c> is refused, so this
/// cannot become a second way to approve something — the self-review rule stays where it is,
/// in the aggregate, guarding the decision. This only finishes a decision somebody else made.
/// </para>
/// </summary>
[Authorize(Permission = TrustPermissions.ReviewVerification)]
[Transactional]
public sealed record ReapplyVerificationOutcomeCommand : ICommand<VerificationResponse>
{
    public required Guid VerificationId { get; init; }
}

public sealed class ReapplyVerificationOutcomeCommandHandler
    : ICommandHandler<ReapplyVerificationOutcomeCommand, VerificationResponse>
{
    private readonly IVerificationRequestRepository _verifications;
    private readonly TrustProfileProvisioner _provisioner;
    private readonly VerificationGrantApplier _grants;
    private readonly BadgeEvaluator _badges;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<ReapplyVerificationOutcomeCommandHandler> _logger;

    public ReapplyVerificationOutcomeCommandHandler(
        IVerificationRequestRepository verifications,
        TrustProfileProvisioner provisioner,
        VerificationGrantApplier grants,
        BadgeEvaluator badges,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<ReapplyVerificationOutcomeCommandHandler> logger)
    {
        _verifications = verifications;
        _provisioner = provisioner;
        _grants = grants;
        _badges = badges;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Result<VerificationResponse>> Handle(
        ReapplyVerificationOutcomeCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var verification = await _verifications.GetAsync(
            VerificationRequestId.From(request.VerificationId), cancellationToken);

        if (verification is null)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.VerificationNotFound);
        }

        if (verification.Status != VerificationStatus.Approved)
        {
            return Result.Failure<VerificationResponse>(TrustErrors.VerificationNotApproved);
        }

        var (profile, reputation) = await _provisioner.GetOrCreateBothAsync(
            verification.UserId, cancellationToken);

        var country = verification.TargetCountry ?? verification.ResidenceCountry;

        // Already applied, so there is nothing to finish. Refused rather than repeated, because
        // the award behind a country verification declares no MaxOccurrences: running this twice
        // would pay for the same approval twice, which is the exact defect this module has
        // produced three times already. See ReputationAwardConsumers.
        if (AlreadyApplied(profile, verification.Type, country))
        {
            return Result.Failure<VerificationResponse>(TrustErrors.VerificationAlreadyApplied);
        }

        var now = _clock.UtcNow;

        await _grants.ApplyApprovalAsync(
            verification,
            "Your verification was approved. Its effect on your standing has now been applied.",
            now,
            cancellationToken);

        _badges.Reconcile(profile, reputation, profile.HasBadge(BadgeType.Moderator), now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Re-applied the outcome of {Type} verification {VerificationId} for {UserId}. "
            + "An approval reached the member's standing late, which means something set the "
            + "status outside ReviewVerification.",
            verification.Type,
            verification.Id.Value,
            verification.UserId.Value);

        return Result.Success(verification.ToResponse());
    }

    /// <summary>
    /// Whether this approval already reached the member's standing.
    /// <para>
    /// Country and residence claims are checked against the standing for <em>that</em> country
    /// rather than against the grant set, because the grant set is keyed by type: a member
    /// verified in one country holds <c>Country</c>, and a type-only check would refuse to
    /// finish an approval for their second one. That case is real — see the relocation flow.
    /// </para>
    /// </summary>
    private static bool AlreadyApplied(TrustProfile profile, VerificationType type, string? country)
    {
        if (type is VerificationType.Country or VerificationType.Residence)
        {
            return country is not null
                && profile.Countries.Any(standing =>
                    string.Equals(standing.CountryCode, country, StringComparison.OrdinalIgnoreCase)
                    && standing.IsVerified);
        }

        return profile.Grants.Contains(type);
    }
}
