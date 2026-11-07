using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Identity.Events;
using Diaspora.Platform.Messaging;
using Microsoft.Extensions.Logging;

namespace Diaspora.Application.Identity.EventHandlers;

/// <summary>
/// Audits and announces every movement along the progressive access ladder.
/// <para>
/// This is the event other modules should subscribe to. Individual lifecycle events
/// (suspended, verified, reinstated) each describe one cause; this one describes the
/// consequence, which is what determines what a member may actually do.
/// </para>
/// </summary>
public sealed class UserAccessStateChangedEventHandler : DomainEventHandler<UserAccessStateChangedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;
    private readonly IIntegrationEventPublisher _integrationEvents;
    private readonly ILogger<UserAccessStateChangedEventHandler> _logger;

    public UserAccessStateChangedEventHandler(
        IIdentityAuditWriter audit,
        IIntegrationEventPublisher integrationEvents,
        ILogger<UserAccessStateChangedEventHandler> logger)
    {
        _audit = audit;
        _integrationEvents = integrationEvents;
        _logger = logger;
    }

    protected override async Task HandleAsync(
        UserAccessStateChangedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "User {UserId} moved from {FromState} to {ToState} ({Reason})",
            domainEvent.UserId, domainEvent.FromState, domainEvent.ToState, domainEvent.Reason);

        await _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.AccessStateChanged,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.Value.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string>
            {
                ["from"] = domainEvent.FromState,
                ["to"] = domainEvent.ToState,
                ["reason"] = domainEvent.Reason.ToString(),
                ["notes"] = domainEvent.Notes ?? string.Empty
            }
        }, cancellationToken);

        await _integrationEvents.PublishAsync(new UserAccessStateChangedIntegrationEvent
        {
            UserId = domainEvent.UserId.Value,
            FromState = domainEvent.FromState,
            ToState = domainEvent.ToState,
            Reason = domainEvent.Reason.ToString(),
            // Only what staff wrote about a staff action reaches the member; the ladder's own
            // moves (registration, verification) carry no note.
            Note = domainEvent.Reason is AccessStateChangeReason.ModerationSuspension
                or AccessStateChangeReason.ModerationReinstatement
                or AccessStateChangeReason.AccountDisabled
                or AccessStateChangeReason.AccountReactivated
                ? domainEvent.Notes
                : null,
            Until = domainEvent.Until
        }, cancellationToken);
    }
}
