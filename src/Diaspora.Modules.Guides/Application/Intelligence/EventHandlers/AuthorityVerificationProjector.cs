using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Guides.Domain.Embassies;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Modules.Guides.Infrastructure.Trust;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Guides.Application.Intelligence.EventHandlers;

/// <summary>
/// The only path by which a Trust decision reaches an authority's or a mission's verified badge.
/// <para>
/// <b>One projector for both registers, because a Trust decision does not say which it belongs
/// to.</b> It arrives carrying a verification identifier this module chose to store and nothing else
/// that identifies the subject — so the correlation is a lookup, and the lookup has to try both. Two
/// projectors would both run on every Trust event on the platform and one of them would always miss.
/// </para>
/// <para>
/// <b>The case identifier is stored at submission time, and that is the whole integration.</b> B8
/// shipped its equivalent without storing it: submissions went out, Trust decided, approvals came
/// back, and every one of them had nowhere to land — each half correct in isolation, which is exactly
/// why only an end-to-end test that submits, drains the outbox and asserts the badge changed can
/// catch it. This is that defect not repeated.
/// </para>
/// <para>
/// <b>A miss is silence, not an error.</b> Trust raises <c>Embassy</c> verifications for anybody who
/// wants them, and it raises phone, business and diaspora verifications several times a minute in a
/// healthy system. A projector that logged a warning for every event that was not its own would
/// produce continuous noise and hide the one that matters.
/// </para>
/// </summary>
public sealed class AuthorityVerificationProjector
{
    private readonly IIssuingAuthorityRepository _authorities;
    private readonly IEmbassyRepository _embassies;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly ILogger<AuthorityVerificationProjector> _logger;

    public AuthorityVerificationProjector(
        IIssuingAuthorityRepository authorities,
        IEmbassyRepository embassies,
        IUnitOfWork unitOfWork,
        IClock clock,
        ILogger<AuthorityVerificationProjector> logger)
    {
        _authorities = authorities;
        _embassies = embassies;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _logger = logger;
    }

    /// <summary>
    /// Applies a decision to whichever subject opened the case.
    /// </summary>
    /// <param name="verificationType">
    /// Trust's type name. Checked first because it is the cheap filter, and it is necessary but not
    /// sufficient — another consumer's <c>Embassy</c> verification carries the same type, so what
    /// actually decides is whether the case resolves to a row here.
    /// </param>
    /// <param name="verificationCaseId">The case Trust opened, stored at submission.</param>
    /// <param name="verified">Whether the claim held up.</param>
    public async Task ApplyAsync(
        string? verificationType,
        Guid verificationCaseId,
        bool verified,
        CancellationToken cancellationToken = default)
    {
        if (!AuthorityVerificationTypeMap.IsAuthorityVerification(verificationType))
        {
            return;
        }

        var now = _clock.UtcNow;

        var authority = await _authorities.FindByVerificationCaseAsync(
            verificationCaseId, cancellationToken);

        if (authority is not null)
        {
            authority.ApplyVerification(verified, now);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Authority {AuthorityId} verification set to {Verified} from Trust case {CaseId}.",
                authority.Id.Value,
                verified,
                verificationCaseId);

            return;
        }

        var embassy = await _embassies.FindByVerificationCaseAsync(
            verificationCaseId, cancellationToken);

        if (embassy is null)
        {
            // Somebody else's Embassy verification. Silence is correct.
            return;
        }

        embassy.ApplyVerification(verified, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Embassy {EmbassyId} verification set to {Verified} from Trust case {CaseId}.",
            embassy.Id.Value,
            verified,
            verificationCaseId);
    }
}
