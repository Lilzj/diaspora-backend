using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Domain.Events;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// Publishes a post edit.
/// <para>
/// Search re-indexes on this. A notification module deliberately should not — telling every
/// reader that a typo was fixed is how a feed teaches members to mute it.
/// </para>
/// </summary>
public sealed class PostEditedEventHandler : DomainEventHandler<PostEditedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;

    public PostEditedEventHandler(CommunityIntegrationEventRelay relay) => _relay = relay;

    protected override Task HandleAsync(
        PostEditedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _relay.PublishAsync(
            new CommunityPostUpdatedIntegrationEvent
            {
                PostId = domainEvent.PostId.Value,
                EditedBy = domainEvent.EditedBy.Value,
                RevisionNumber = domainEvent.RevisionNumber,
                IsMarkedEdited = domainEvent.IsMarkedEdited
            },
            cancellationToken);
    }
}
