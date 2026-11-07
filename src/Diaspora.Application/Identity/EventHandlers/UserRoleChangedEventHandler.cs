using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Identity.Events;
using Diaspora.Platform.Messaging;

namespace Diaspora.Application.Identity.EventHandlers;

/// <summary>
/// Records a role change and announces it, so the member is told (7 Nov 2026).
/// <para>
/// Before this a role changed in silence: a member made a Moderator, or stripped of being one,
/// found out by what the app let them do.
/// </para>
/// </summary>
public sealed class UserRoleChangedEventHandler : DomainEventHandler<UserRoleChangedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;
    private readonly IIntegrationEventPublisher _integrationEvents;

    public UserRoleChangedEventHandler(
        IIdentityAuditWriter audit,
        IIntegrationEventPublisher integrationEvents)
    {
        _audit = audit;
        _integrationEvents = integrationEvents;
    }

    protected override async Task HandleAsync(
        UserRoleChangedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.RoleChanged,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.Value.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string>
            {
                ["from"] = domainEvent.PreviousRole,
                ["to"] = domainEvent.NewRole,
                ["reason"] = domainEvent.Reason ?? string.Empty
            }
        }, cancellationToken);

        await _integrationEvents.PublishAsync(new UserRoleChangedIntegrationEvent
        {
            UserId = domainEvent.UserId.Value,
            PreviousRole = domainEvent.PreviousRole,
            NewRole = domainEvent.NewRole,
            Reason = domainEvent.Reason
        }, cancellationToken);
    }
}
