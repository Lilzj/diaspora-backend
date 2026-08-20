using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Trust.V1.Events;
using Diaspora.Modules.Marketplace.Application.Services;
using Diaspora.Modules.Marketplace.Domain.Enums;

namespace Diaspora.Modules.Marketplace.Application.EventHandlers;

/// <summary>
/// Applies Trust's approval to whichever seller or listing raised the claim.
/// <para>
/// <b>The only path to a verified mark anywhere in B10.</b> Nothing a seller does can set one; this
/// handler is reachable only from a published Trust contract, which is what makes "Marketplace never
/// owns Trust data" enforceable rather than merely stated.
/// </para>
/// <para>
/// The work itself lives in <see cref="TrustOutcomeApplier"/>, shared with the handler for the three
/// negative outcomes. See that class for why it is a service rather than a method here.
/// </para>
/// </summary>
public sealed class TrustVerificationOutcomeHandler
    : IntegrationEventHandler<VerificationApprovedIntegrationEvent>
{
    private readonly TrustOutcomeApplier _outcomes;

    public TrustVerificationOutcomeHandler(TrustOutcomeApplier outcomes) => _outcomes = outcomes;

    protected override Task HandleAsync(
        VerificationApprovedIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // A partial approval is not a badge. Trust says the claim is partly substantiated, and
        // rendering "Verified" from that would be the platform overstating what it checked.
        if (integrationEvent.Partial)
        {
            return Task.CompletedTask;
        }

        return _outcomes.ApplyAsync(
            integrationEvent.VerificationId,
            ListingVerificationState.Verified,

            // Trust's own type name is the badge label. B10 renders it and never interprets it,
            // which is what lets Trust add a verification type without B10 changing.
            badge: integrationEvent.VerificationType,
            reason: null,
            integrationEvent.ApprovedAt,
            cancellationToken);
    }
}
