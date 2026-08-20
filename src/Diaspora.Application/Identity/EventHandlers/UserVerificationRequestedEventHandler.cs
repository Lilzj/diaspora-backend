using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Application.Identity.Auditing;
using Diaspora.Application.Identity.Models;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Identity.Events;
using Diaspora.Platform.Messaging;

namespace Diaspora.Application.Identity.EventHandlers;

/// <summary>
/// Hands a verification request to the Trust &amp; Verification Engine.
/// <para>
/// This is the outbound half of the B2/B3 boundary: Identity says "this member asked",
/// and B3 replies through the trust decision endpoint. Identity never waits for, models,
/// or second-guesses the verdict.
/// </para>
/// </summary>
public sealed class UserVerificationRequestedEventHandler
    : DomainEventHandler<UserVerificationRequestedDomainEvent>
{
    private readonly IIdentityAuditWriter _audit;
    private readonly IIntegrationEventPublisher _integrationEvents;

    public UserVerificationRequestedEventHandler(
        IIdentityAuditWriter audit,
        IIntegrationEventPublisher integrationEvents)
    {
        _audit = audit;
        _integrationEvents = integrationEvents;
    }

    protected override async Task HandleAsync(
        UserVerificationRequestedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        await _audit.WriteAsync(new IdentityAuditDraft
        {
            Action = IdentityAuditActions.VerificationRequested,
            EntityType = IdentityAuditActions.EntityTypes.User,
            EntityId = domainEvent.UserId.ToString(),
            SubjectUserId = domainEvent.UserId
        }, cancellationToken);

        await _integrationEvents.PublishAsync(new UserVerificationRequestedIntegrationEvent
        {
            UserId = domainEvent.UserId.Value,
            CountryOfResidence = domainEvent.CountryOfResidence,
            Nationality = domainEvent.Nationality,
            RequestedAt = domainEvent.RequestedAt
        }, cancellationToken);
    }
}
