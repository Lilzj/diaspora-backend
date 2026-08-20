using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Guides.Application.Intelligence.EventHandlers;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// A verification lapsed. The mark comes off.
/// <para>
/// Expiry is projected as revocation deliberately. To a reader the two are the same fact — this
/// guide is no longer verified — and a state that said "verified, but expired" would be rendered
/// as a badge by every consumer that did not think to check a second field. Guidance about rules
/// that change annually is exactly the content whose verification should lapse.
/// </para>
/// </summary>
public sealed class GuideVerificationExpiredEventHandler
    : IntegrationEventHandler<VerificationExpiredIntegrationEvent>
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

    public GuideVerificationExpiredEventHandler(
        GuideTrustVerificationProjector projector,
        AuthorityVerificationProjector authorities)
    {
        _projector = projector;
        _authorities = authorities;
    }

    protected override async Task HandleAsync(
        VerificationExpiredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _projector.ApplyAsync(
            integrationEvent.VerificationId,
            integrationEvent.VerificationType,
            GuideVerificationState.Revoked,
            integrationEvent.ExpiredAt,
            reason: "The verification expired.",
            cancellationToken: cancellationToken);

        await _authorities.ApplyAsync(
            integrationEvent.VerificationType,
            integrationEvent.VerificationId,
            verified: false,
            cancellationToken);
    }
}
