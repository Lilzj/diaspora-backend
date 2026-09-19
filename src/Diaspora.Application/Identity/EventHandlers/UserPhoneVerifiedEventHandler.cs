using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Identity.Events;
using Diaspora.Platform.Messaging;

namespace Diaspora.Application.Identity.EventHandlers;

/// <summary>
/// Audits the phone verification, and tells the rest of the platform it happened.
/// <para>
/// <b>It only audited.</b> Trust keeps its own record of which verifications a member holds, and
/// nothing ever told it about a phone — so <c>verifications.Phone</c> was seeded <c>Pending</c>
/// for every member and stayed there for ever, including a member who had verified their phone
/// thirty seconds earlier. The trust centre showed all of them a "Phone Linked — Verify" row,
/// and the three capabilities that declare <c>RequiresPhone</c> were unsatisfiable by anyone.
/// </para>
/// </summary>
public sealed class UserPhoneVerifiedEventHandler : DomainEventHandler<UserPhoneVerifiedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;
    private readonly IIntegrationEventPublisher _integrationEvents;

    public UserPhoneVerifiedEventHandler(
        IIdentityAuditWriter audit, IIntegrationEventPublisher integrationEvents)
    {
        _audit = audit;
        _integrationEvents = integrationEvents;
    }

    protected override async Task HandleAsync(
        UserPhoneVerifiedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.PhoneVerified,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.ToString(),
            SubjectUserId = domainEvent.UserId
        }, cancellationToken);

        await _integrationEvents.PublishAsync(
            new UserPhoneVerifiedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                PhoneNumber = domainEvent.PhoneNumber,
                VerifiedAt = domainEvent.VerifiedAt
            },
            cancellationToken);
    }
}
