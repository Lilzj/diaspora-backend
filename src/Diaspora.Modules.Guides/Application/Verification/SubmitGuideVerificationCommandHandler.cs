using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Application.Services;
using Diaspora.Modules.Guides.Contracts.V1.Responses;
using Diaspora.Modules.Guides.Domain.Errors;
using Diaspora.Modules.Guides.Domain.Guides;
using Diaspora.Modules.Guides.Extensibility.Verification;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

using GuideId = Diaspora.Modules.Guides.Domain.Identifiers.GuideId;

namespace Diaspora.Modules.Guides.Application.Verification;

/// <summary>
/// Hands a verification claim to whatever this deployment runs, and records that it went.
/// <para>
/// The registry is asked first, exactly as B7's provider submission does: a claim the guide
/// records but nothing actually holds is a verification that never resolves and that nobody can
/// find to chase. When nothing can decide, the command fails with
/// <c>guides.verification.unavailable</c> and the guide is left untouched.
/// </para>
/// <para>
/// <b>The deciding system's case identifier is stamped on the trust mark as the claim is
/// recorded.</b> That is what lets the outcome find its way home: Trust's events name a member,
/// a member may have many guides, and only the case identifier says which one raised this claim.
/// Submitting without recording it would leave every approval arriving with nowhere to land.
/// </para>
/// </summary>
public sealed class SubmitGuideVerificationCommandHandler
    : ICommandHandler<SubmitGuideVerificationCommand, GuideVerificationStatusResponse>
{
    private readonly IGuideRepository _guides;
    private readonly GuideVerificationProviderRegistry _verifiers;
    private readonly IUnitOfWork _unitOfWork;
    private readonly GuideAuthorizationService _authorization;
    private readonly IClock _clock;

    public SubmitGuideVerificationCommandHandler(
        IGuideRepository guides,
        GuideVerificationProviderRegistry verifiers,
        IUnitOfWork unitOfWork,
        GuideAuthorizationService authorization,
        IClock clock)
    {
        _guides = guides;
        _verifiers = verifiers;
        _unitOfWork = unitOfWork;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<GuideVerificationStatusResponse>> Handle(
        SubmitGuideVerificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var guide = await _guides.GetAsync(GuideId.From(command.GuideId), cancellationToken);

        if (guide is null)
        {
            return Result.Failure<GuideVerificationStatusResponse>(GuideErrors.NotFound);
        }

        // Ownership, not merely permission. The permission says this member may submit
        // verifications; this says they may submit one for *this* guide.
        var caller = _authorization.RequireAuthor(guide);

        if (caller.IsFailure)
        {
            return Result.Failure<GuideVerificationStatusResponse>(caller.Error);
        }

        if (!_verifiers.CanSubmit)
        {
            return Result.Failure<GuideVerificationStatusResponse>(
                GuideErrors.VerificationUnavailable);
        }

        var outcome = await _verifiers.SubmitAsync(
            BuildRequest(guide, caller.Value.Value, command.Request), cancellationToken);

        if (outcome.IsFailure)
        {
            return Result.Failure<GuideVerificationStatusResponse>(outcome.Error);
        }

        var now = _clock.UtcNow;
        var submitted = guide.SubmitForVerification(
            now, caller.Value, outcome.Value.VerificationId);

        if (submitted.IsFailure)
        {
            return Result.Failure<GuideVerificationStatusResponse>(submitted.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new GuideVerificationStatusResponse
        {
            GuideId = guide.Id.Value,
            State = GuideWireNames.ToWire(guide.TrustMark.State),
            VerificationId = outcome.Value.VerificationId ?? guide.TrustMark.VerificationId,
            Badge = guide.TrustMark.Badge,
            Reason = guide.TrustMark.Reason,
            VerifiedAt = guide.TrustMark.VerifiedAt,
            ChangedAt = guide.TrustMark.ChangedAt
        });
    }

    /// <summary>
    /// Flattens the aggregate into what a verifier is told. Flat primitives rather than the
    /// <c>Guide</c> itself, which is the seam's whole point — a provider must not be able to
    /// mutate what it was asked to check.
    /// </summary>
    private static GuideVerificationRequest BuildRequest(
        Guide guide, Guid submittedBy, Contracts.V1.Requests.SubmitGuideVerificationRequest request) =>
        new()
        {
            GuideId = guide.Id.Value,
            SubmittedBy = submittedBy,
            AuthorId = guide.AuthorId.Value,
            Title = guide.Title.Value,
            Slug = guide.Slug.Value,
            CategoryKey = guide.CategoryKey,
            Language = guide.Language.Code,
            CountryCode = guide.Location.CountryCode,
            Version = guide.CurrentVersion,
            Evidence = request.Evidence
                .Select(evidence => new GuideVerificationEvidence
                {
                    MediaId = evidence.MediaId,
                    DocumentType = evidence.DocumentType
                })
                .ToList(),
            Notes = request.Notes
        };
}
