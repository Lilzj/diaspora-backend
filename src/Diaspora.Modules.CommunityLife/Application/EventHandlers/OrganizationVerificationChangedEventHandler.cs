using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Domain.Events;

namespace Diaspora.Modules.CommunityLife.Application.EventHandlers;

/// <summary>
/// Trust decided something about a body. Published on every outcome, not only approval — a cached
/// badge that outlives the verification behind it is the one a member would act on wrongly.
/// </summary>
public sealed class OrganizationVerificationChangedEventHandler
    : DomainEventHandler<OrganizationVerificationChangedDomainEvent>
{
    private readonly CommunityLifeIntegrationEventRelay _relay;

    public OrganizationVerificationChangedEventHandler(
        CommunityLifeIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        OrganizationVerificationChangedDomainEvent domainEvent,
        CancellationToken cancellationToken) =>
        _relay.PublishAsync(domainEvent, cancellationToken);
}
