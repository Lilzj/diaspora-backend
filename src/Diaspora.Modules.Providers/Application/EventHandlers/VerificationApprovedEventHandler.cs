using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>
/// Trust granted a verification. Projects it onto the provider that claimed it.
/// <para>
/// Subscribed through <c>DomainEventHandler&lt;T&gt;</c> because
/// <c>IIntegrationEvent</c> derives from <c>IDomainEvent</c> and the platform dispatches both
/// through the same wrapper — which is what lets a module consume another's published events
/// without either knowing the other exists.
/// </para>
/// </summary>
public sealed class VerificationApprovedEventHandler
    : IntegrationEventHandler<VerificationApprovedIntegrationEvent>
{
    private readonly TrustVerificationProjector _projector;

    public VerificationApprovedEventHandler(TrustVerificationProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        VerificationApprovedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.ApplyAsync(
            integrationEvent.UserId,
            integrationEvent.VerificationId,
            integrationEvent.VerificationType,
            ProviderVerificationState.Verified,
            integrationEvent.ApprovedAt,
            integrationEvent.ExpiresAt,
            cancellationToken: cancellationToken);
    }
}
