using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Guides.Application.Intelligence.EventHandlers;
using Diaspora.Modules.Guides.Domain.Enums;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Trust granted a verification. Projects it onto the guide that claimed it.
/// <para>
/// Derives from <see cref="IntegrationEventHandler{T}"/> rather than the domain-event base,
/// which since B1.2 is what the compiler enforces: this is a published contract from another
/// module, delivered out of band, at-least-once and unordered — a different pipeline with
/// different guarantees.
/// </para>
/// </summary>
public sealed class GuideVerificationApprovedEventHandler
    : IntegrationEventHandler<VerificationApprovedIntegrationEvent>
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

    public GuideVerificationApprovedEventHandler(
        GuideTrustVerificationProjector projector,
        AuthorityVerificationProjector authorities)
    {
        _projector = projector;
        _authorities = authorities;
    }

    protected override async Task HandleAsync(
        VerificationApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _projector.ApplyAsync(
            integrationEvent.VerificationId,
            integrationEvent.VerificationType,
            GuideVerificationState.Verified,
            integrationEvent.ApprovedAt,
            cancellationToken: cancellationToken);

        await _authorities.ApplyAsync(
            integrationEvent.VerificationType,
            integrationEvent.VerificationId,
            verified: true,
            cancellationToken);
    }
}
