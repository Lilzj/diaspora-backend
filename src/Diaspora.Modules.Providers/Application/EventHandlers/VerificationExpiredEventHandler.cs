using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// A grant lapsed. Projects the lapse so the badge comes off.
/// <para>
/// The sweeper catches lapses too, by expiry date, and this handler is the fast path: a
/// verification that expires while Trust is watching should stop showing a badge within seconds
/// rather than within the sweep interval.
/// </para>
/// </summary>
public sealed class VerificationExpiredEventHandler
    : IntegrationEventHandler<VerificationExpiredIntegrationEvent>
{
    private readonly TrustVerificationProjector _projector;

    public VerificationExpiredEventHandler(TrustVerificationProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        VerificationExpiredIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.ApplyAsync(
            integrationEvent.UserId,
            integrationEvent.VerificationId,
            integrationEvent.VerificationType,
            ProviderVerificationState.Expired,
            integrationEvent.ExpiredAt,
            cancellationToken: cancellationToken);
    }
}
