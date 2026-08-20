using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Providers.Domain.Enums;

namespace Diaspora.Modules.Providers.Application.EventHandlers;

/// <summary>Trust refused a verification. Projects the refusal, with its reason code.</summary>
public sealed class VerificationRejectedEventHandler
    : IntegrationEventHandler<VerificationRejectedIntegrationEvent>
{
    private readonly TrustVerificationProjector _projector;

    public VerificationRejectedEventHandler(TrustVerificationProjector projector) =>
        _projector = projector;

    protected override Task HandleAsync(
        VerificationRejectedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _projector.ApplyAsync(
            integrationEvent.UserId,
            integrationEvent.VerificationId,
            integrationEvent.VerificationType,
            ProviderVerificationState.Rejected,
            integrationEvent.RejectedAt,
            reasonCode: integrationEvent.ReasonCode,
            reason: integrationEvent.Details,
            cancellationToken: cancellationToken);
    }
}
