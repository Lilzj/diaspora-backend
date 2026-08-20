using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;

namespace Diaspora.Modules.Guides.Application.EventHandlers;

/// <summary>
/// Trust moved a member's level. Mirrors the label onto the author's standing.
/// <para>
/// Stored as an opaque string and never branched on. The moment B8 interpreted a level — gating a
/// capability on it, ranking by it — Trust's scoring model would become part of B8's behaviour,
/// and a change there would silently change what guides readers see.
/// </para>
/// </summary>
public sealed class GuideAuthorTrustLevelChangedEventHandler
    : IntegrationEventHandler<TrustLevelChangedIntegrationEvent>
{
    private readonly GuideAuthorStandingProjector _projector;

    public GuideAuthorTrustLevelChangedEventHandler(GuideAuthorStandingProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        TrustLevelChangedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.ApplyTrustLevelAsync(
            integrationEvent.UserId,
            integrationEvent.CurrentLevel,
            integrationEvent.ChangedAt,
            cancellationToken);
    }
}
