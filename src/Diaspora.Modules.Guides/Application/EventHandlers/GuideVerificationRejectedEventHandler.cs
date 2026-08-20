using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Guides.Application.Intelligence.EventHandlers;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Trust refused a verification. Clears the guide's pending mark.
/// <para>
/// Rejection returns the guide to <see cref="GuideVerificationState.None"/> rather than to
/// <c>Revoked</c>: nothing was ever granted, and marking an unverified guide as revoked would
/// tell readers something was taken away. The reason travels with it so the author can see why
/// and resubmit with better evidence.
/// </para>
/// </summary>
public sealed class GuideVerificationRejectedEventHandler
    : IntegrationEventHandler<VerificationRejectedIntegrationEvent>
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

    public GuideVerificationRejectedEventHandler(
        GuideTrustVerificationProjector projector,
        AuthorityVerificationProjector authorities)
    {
        _projector = projector;
        _authorities = authorities;
    }

    protected override async Task HandleAsync(
        VerificationRejectedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _projector.ApplyAsync(
            integrationEvent.VerificationId,
            integrationEvent.VerificationType,
            GuideVerificationState.None,
            integrationEvent.RejectedAt,
            reason: integrationEvent.Details ?? integrationEvent.ReasonCode,
            cancellationToken: cancellationToken);

        await _authorities.ApplyAsync(
            integrationEvent.VerificationType,
            integrationEvent.VerificationId,
            verified: false,
            cancellationToken);
    }
}
