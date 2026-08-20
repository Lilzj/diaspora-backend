using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// A grant was withdrawn for cause. Projects the revocation.
/// <para>
/// The event that matters most to get right in the negative direction: a badge left on a
/// business whose registration was withdrawn is worse than never having shown one, because a
/// member relied on it.
/// </para>
/// </summary>
public sealed class VerificationRevokedEventHandler
    : IntegrationEventHandler<VerificationRevokedIntegrationEvent>
{
    private readonly TrustVerificationProjector _projector;

    public VerificationRevokedEventHandler(TrustVerificationProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        VerificationRevokedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.ApplyAsync(
            integrationEvent.UserId,
            integrationEvent.VerificationId,
            integrationEvent.VerificationType,
            ProviderVerificationState.Revoked,
            integrationEvent.RevokedAt,
            reason: integrationEvent.Reason,
            cancellationToken: cancellationToken);
    }
}
