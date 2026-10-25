using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Identity.Events;
using Diaspora.Platform.Messaging;

namespace Diaspora.Application.Identity.EventHandlers;

/// <summary>
/// Writes the registration audit entry and announces the new member to the platform.
/// <para>
/// Domain event handlers run after the transaction commits, so they must not touch
/// repositories — nothing would persist. They write to the audit trail and the
/// integration bus, both of which are outside the unit of work by design.
/// </para>
/// </summary>
public sealed class UserRegisteredEventHandler : DomainEventHandler<UserRegisteredDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;
    private readonly IIntegrationEventPublisher _integrationEvents;

    public UserRegisteredEventHandler(
        IIdentityAuditWriter audit,
        IIntegrationEventPublisher integrationEvents)
    {
        _audit = audit;
        _integrationEvents = integrationEvents;
    }

    protected override async Task HandleAsync(
        UserRegisteredDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.UserRegistered,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.ToString(),
            SubjectUserId = domainEvent.UserId,
            Metadata = new Dictionary<string, string>
            {
                ["nationality"] = domainEvent.Nationality,
                ["countryOfResidence"] = domainEvent.CountryOfResidence,
                ["language"] = domainEvent.PreferredLanguage
            }
        }, cancellationToken);

        await _integrationEvents.PublishAsync(new UserRegisteredIntegrationEvent
        {
            UserId = domainEvent.UserId.Value,
            FullName = domainEvent.FullName,
            Nationality = domainEvent.Nationality,
            CountryOfResidence = domainEvent.CountryOfResidence,
            PreferredLanguage = domainEvent.PreferredLanguage,
            ReferralCode = domainEvent.ReferralCode
        }, cancellationToken);
    }
}
