using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Observability;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// Publishes a moderation decision.
/// <para>
/// A decision of <c>suspend</c> reaches Identity through this event and is never performed here.
/// Recording that a moderator decided to suspend is not the same as suspending, and the
/// difference is which module is accountable for the account.
/// </para>
/// </summary>
public sealed class ModerationDecidedEventHandler : DomainEventHandler<ModerationDecidedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public ModerationDecidedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        ModerationDecidedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        CommunityTelemetry.ModerationDecisions.Add(
            1,
            new KeyValuePair<string, object?>(
                "action", CommunityWireNames.ToWire(domainEvent.Action)),
            new KeyValuePair<string, object?>(
                "source", CommunityWireNames.ToWire(domainEvent.Source)));

        return _relay.PublishAsync(
            new CommunityModerationDecidedIntegrationEvent
            {
                DecisionId = domainEvent.DecisionId.Value,
                ContentType = CommunityWireNames.ToWire(domainEvent.Content.Type),
                ContentId = domainEvent.Content.Id,
                Action = CommunityWireNames.ToWire(domainEvent.Action),
                Source = CommunityWireNames.ToWire(domainEvent.Source),
                ModeratorId = domainEvent.ModeratorId,
                ContentAuthorId = domainEvent.ContentAuthorId,
                Reason = domainEvent.Reason
            },
            cancellationToken);
    }
}
