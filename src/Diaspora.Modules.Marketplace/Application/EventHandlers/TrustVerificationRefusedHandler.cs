using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Enums;
using MediatR;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Applies Trust's three negative outcomes — rejected, revoked and expired.
/// <para>
/// <b>Revocation is the one that matters most.</b> A seller whose verification is withdrawn must
/// stop being a "Verified Business" on every card in the same beat, and a module that only listened
/// for approvals would leave the platform vouching for exactly the wrong party — which on a
/// marketplace is the most damaging thing a stale badge can do.
/// </para>
/// <para>
/// The three are one class because they are one action against three names, and the work is shared
/// with the approval handler through <see cref="TrustOutcomeApplier"/> so all four land through the
/// same guarded write.
/// </para>
/// </summary>
public sealed class TrustVerificationRefusedHandler :
    INotificationHandler<IntegrationEventNotification<VerificationRejectedIntegrationEvent>>,
    INotificationHandler<IntegrationEventNotification<VerificationRevokedIntegrationEvent>>,
    INotificationHandler<IntegrationEventNotification<VerificationExpiredIntegrationEvent>>
{
    private readonly TrustOutcomeApplier _outcomes;

    public TrustVerificationRefusedHandler(TrustOutcomeApplier outcomes) => _outcomes = outcomes;

    public Task Handle(
        IntegrationEventNotification<VerificationRejectedIntegrationEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var integrationEvent = notification.IntegrationEvent;

        return _outcomes.ApplyAsync(
            integrationEvent.VerificationId,
            ListingVerificationState.Rejected,
            badge: null,

            // Trust's own reason, passed through unchanged. Replacing it with a generic message
            // would leave the claimant unable to fix what was wrong.
            integrationEvent.Details ?? integrationEvent.ReasonCode,
            integrationEvent.RejectedAt,
            cancellationToken);
    }

    public Task Handle(
        IntegrationEventNotification<VerificationRevokedIntegrationEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var integrationEvent = notification.IntegrationEvent;

        return _outcomes.ApplyAsync(
            integrationEvent.VerificationId,
            ListingVerificationState.Revoked,
            badge: null,
            integrationEvent.Reason,
            integrationEvent.RevokedAt,
            cancellationToken);
    }

    /// <summary>
    /// An expiry clears the mark rather than rejecting it. Trust did not refuse anything — the
    /// evidence simply aged out — and telling a seller they were rejected would be wrong.
    /// </summary>
    public Task Handle(
        IntegrationEventNotification<VerificationExpiredIntegrationEvent> notification,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(notification);

        var integrationEvent = notification.IntegrationEvent;

        return _outcomes.ApplyAsync(
            integrationEvent.VerificationId,
            ListingVerificationState.None,
            badge: null,
            "The verification expired. Submit it again to restore the badge.",
            integrationEvent.ExpiredAt,
            cancellationToken);
    }
}
