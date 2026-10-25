using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>Publishes a verification threshold crossing. A ranking cache must not ignore it.</summary>
public sealed class PostVerificationChangedEventHandler
    : DomainEventHandler<PostVerificationChangedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public PostVerificationChangedEventHandler(CommunityIntegrationEventRelay relay) =>
        _relay = relay;

    protected override Task HandleAsync(
        PostVerificationChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityPostVerificationChangedIntegrationEvent
            {
                PostId = domainEvent.PostId.Value,
                Previous = CommunityWireNames.ToWire(domainEvent.Previous),
                Current = CommunityWireNames.ToWire(domainEvent.Current),
                Confirmations = domainEvent.Confirmations,
                VerifiedConfirmations = domainEvent.VerifiedConfirmations,
                AuthorUserId = domainEvent.AuthorId.Value,
                CountryCode = domainEvent.CountryCode
            },
            cancellationToken);
    }
}
