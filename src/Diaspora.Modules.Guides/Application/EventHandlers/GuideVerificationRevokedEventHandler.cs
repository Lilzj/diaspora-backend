using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Guides.Application.Intelligence.EventHandlers;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Trust withdrew a verification it had granted. Revokes the guide's mark.
/// <para>
/// <b>The event this whole projection exists for.</b> Verified advice that has since been found
/// wrong is more dangerous than unverified advice, because a reader trusts it more. Revocation is
/// distinct from rejection: something <em>was</em> granted and is being taken back, and the mark
/// records that rather than pretending it never happened.
/// </para>
/// </summary>
public sealed class GuideVerificationRevokedEventHandler
    : IntegrationEventHandler<VerificationRevokedIntegrationEvent>
{
    private readonly GuideTrustVerificationProjector _projector;

    /// <summary>
    /// B8.2's projector, fanned out to from the same handler.
    /// <para>
    /// <b>One module subscribes to one published contract exactly once, and the platform
    /// fails the build otherwise.</b> Two handlers for one Trust decision would be two
    /// subscriptions racing over the same delivery, and the architecture suite catches it —
    /// which is how B8.2 found out that adding its own was wrong. Both projectors correlate on
    /// the case identifier they stored at submission, so each silently ignores what is not its
    /// own; a Trust decision about a guide reaches the guide, and one about a ministry reaches
    /// the register.
    /// </para>
    /// </summary>
    private readonly AuthorityVerificationProjector _authorities;

    public GuideVerificationRevokedEventHandler(
        GuideTrustVerificationProjector projector,
        AuthorityVerificationProjector authorities)
    {
        _projector = projector;
        _authorities = authorities;
    }

    protected override async Task HandleAsync(
        VerificationRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _projector.ApplyAsync(
            integrationEvent.VerificationId,
            integrationEvent.VerificationType,
            GuideVerificationState.Revoked,
            integrationEvent.RevokedAt,
            reason: integrationEvent.Reason,
            cancellationToken: cancellationToken);

        await _authorities.ApplyAsync(
            integrationEvent.VerificationType,
            integrationEvent.VerificationId,
            verified: false,
            cancellationToken);
    }
}
