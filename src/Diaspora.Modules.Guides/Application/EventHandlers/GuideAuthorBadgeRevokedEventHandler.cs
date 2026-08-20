using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Trust revoked a badge. Removes it from the author's standing.
/// <para>
/// The aggregate refuses the removal when the stored award is newer than this revocation, which
/// under unordered delivery means the badge was re-awarded after the revocation was issued. That
/// guard is what stops a delayed message from stripping a badge somebody has since earned back.
/// </para>
/// </summary>
public sealed class GuideAuthorBadgeRevokedEventHandler
    : IntegrationEventHandler<BadgeRevokedIntegrationEvent>
{
    private readonly GuideAuthorStandingProjector _projector;

    public GuideAuthorBadgeRevokedEventHandler(GuideAuthorStandingProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        BadgeRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.RevokeBadgeAsync(
            integrationEvent.UserId,
            integrationEvent.Badge,
            integrationEvent.CountryCode,
            integrationEvent.RevokedAt,
            cancellationToken);
    }
}
