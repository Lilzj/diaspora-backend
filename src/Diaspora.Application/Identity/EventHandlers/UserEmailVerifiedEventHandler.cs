using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Identity.Events;
using Diaspora.Platform.Messaging;

namespace Diaspora.Application.Identity.EventHandlers;

/// <summary>
/// Tells the rest of the platform that a member's email address is verified.
/// <para>
/// The relay the phone has had since its own mirror was fixed (<see cref="UserPhoneVerifiedEventHandler"/>).
/// Until 20 Oct 2026 nothing published an email verification, so Trust's checklist never showed
/// the Email row as done and its <c>EmailVerificationHandler</c> never ran.
/// </para>
/// <para>
/// No audit row of its own: the profile-updated event raised beside this one already records the
/// change for the audit trail.
/// </para>
/// </summary>
public sealed class UserEmailVerifiedEventHandler : DomainEventHandler<UserEmailVerifiedDomainEvent>
{
    private readonly IIntegrationEventPublisher _integrationEvents;

    public UserEmailVerifiedEventHandler(IIntegrationEventPublisher integrationEvents)
    {
        _integrationEvents = integrationEvents;
    }

    protected override async Task HandleAsync(
        UserEmailVerifiedDomainEvent domainEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        await _integrationEvents.PublishAsync(
            new UserEmailVerifiedIntegrationEvent
            {
                UserId = domainEvent.UserId.Value,
                EmailAddress = domainEvent.EmailAddress,
                VerifiedAt = domainEvent.VerifiedAt
            },
            cancellationToken);
    }
}
