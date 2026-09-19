using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Modules.Trust.Application.Services;
using Diaspora.Modules.Trust.Domain;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Trust.Application.Consumers;

/// <summary>
/// Mirrors Identity's phone verification into Trust.
/// <para>
/// <b>The checklist lied to everybody.</b> <c>VerificationType.Phone</c> is documented as "phone
/// possession, owned by Identity; mirrored here for the checklist" — and nothing mirrored it.
/// Trust seeds <c>verifications.Phone</c> to <c>Pending</c> for every member and changes it only
/// when a Trust verification request of that type is approved, and no caller anywhere created
/// one: the app submits diaspora, identity and country, and nothing else. So the trust centre
/// showed a "Phone Linked — Verify" row to a member who had verified their phone during
/// registration, and went on showing it however many times they did it again.
/// </para>
/// <para>
/// It was not only cosmetic. <c>RequiresPhone</c> is declared by three capabilities —
/// <b>Commenting</b>, <b>Community Updates</b> and the <b>Community Guide Programme</b> — and
/// <c>EligibilityEvaluator</c> reads <c>Grants.Contains(Phone)</c>, which nothing could put
/// there. Nothing enforces those requirements today, so nobody was blocked; the day something
/// does, commenting would have died for every member on the platform at once.
/// </para>
/// <para>
/// <b>Why it submits rather than granting directly.</b> <see cref="PhoneVerificationHandler"/>
/// already exists, needs no document and opts into automatic approval — it was built for exactly
/// this and had nothing to trigger it. Going through
/// <see cref="VerificationSubmissionCoordinator"/> means the grant, the reputation award, the
/// badge reconciliation and the member's trust journey all happen the way they do for every
/// other verification, instead of a second implementation drifting from the first.
/// </para>
/// <para>
/// <b>It must not pay separately.</b> The award for a verified phone is the handler's
/// <c>ApprovalAward</c>, applied once by <see cref="VerificationGrantApplier"/>. A consumer that
/// also scored <c>PhoneVerified</c> stood here until this replaced it, and would have been the
/// third duplicate producer found in this module — see the header of
/// <c>ReputationAwardConsumers.cs</c>.
/// </para>
/// </summary>
public sealed class PhoneVerificationMirrorConsumer
    : IntegrationEventHandler<UserPhoneVerifiedIntegrationEvent>
{
    private readonly VerificationSubmissionCoordinator _submissions;
    private readonly ILogger<PhoneVerificationMirrorConsumer> _logger;

    public PhoneVerificationMirrorConsumer(
        VerificationSubmissionCoordinator submissions,
        ILogger<PhoneVerificationMirrorConsumer> logger)
    {
        _submissions = submissions;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        UserPhoneVerifiedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var userId = UserId.From(integrationEvent.UserId);

        // The correlation identifier is what makes redelivery safe, and it has to be the
        // correlation rather than an open-request check: this type auto-approves, so by the time
        // a duplicate message arrives there is no open request left to find. Derived from the
        // member so it is the same identifier every time, for ever.
        var input = new VerificationSubmissionInput
        {
            UserId = userId,
            Type = VerificationType.Phone,
            CorrelationId = MirrorCorrelationFor(integrationEvent.UserId),
            RequestedBy = "identity-phone-mirror"
        };

        var existing = await _submissions.FindDuplicateAsync(input, cancellationToken);

        if (existing is not null)
        {
            return;
        }

        var submitted = await _submissions.SubmitAsync(input, cancellationToken);

        if (submitted.IsFailure)
        {
            // Worth seeing rather than swallowing: the member's phone is verified in Identity
            // and Trust disagrees, which is the exact state this consumer exists to prevent.
            _logger.LogWarning(
                "Mirroring the verified phone for {UserId} into Trust failed: {Code} {Description}",
                integrationEvent.UserId,
                submitted.Error.Code,
                submitted.Error.Description);
        }
    }

    /// <summary>
    /// A stable identifier for "this member's phone mirror", derived rather than stored.
    /// <para>
    /// Namespaced, because <c>GetByCorrelationAsync</c> looks the identifier up across every
    /// type: handing it the member's own id would make this submission collide with anything
    /// else that ever correlated on the same member.
    /// </para>
    /// </summary>
    internal static Guid MirrorCorrelationFor(Guid userId)
    {
        var seed = System.Text.Encoding.UTF8.GetBytes("trust:identity-phone-mirror:" + userId);
        var hash = System.Security.Cryptography.SHA256.HashData(seed);

        return new Guid(hash.AsSpan(0, 16));
    }
}
