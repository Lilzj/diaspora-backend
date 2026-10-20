using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Abstractions;
using Diaspora.Contracts.Identity.V1.Events;
using Diaspora.Domain.Identity.Events;
using Diaspora.Platform.Messaging;

namespace Diaspora.Application.Identity.EventHandlers;

/// <summary>
/// Tells the platform a member's photo changed, with the address to show.
/// <para>
/// Only a photo that is a Media asset is announced. A free-text key the older
/// <c>PUT /auth/profile</c> field may store has no address, and announcing it as "no photo" would
/// clear a copy other modules hold for no reason the member gave.
/// </para>
/// </summary>
public sealed class UserAvatarChangedEventHandler : DomainEventHandler<UserAvatarChangedDomainEvent>
{
    private readonly IIntegrationEventPublisher _integrationEvents;
    private readonly IAvatarUrlResolver _avatars;

    public UserAvatarChangedEventHandler(
        IIntegrationEventPublisher integrationEvents, IAvatarUrlResolver avatars)
    {
        _integrationEvents = integrationEvents;
        _avatars = avatars;
    }

    protected override async Task HandleAsync(
        UserAvatarChangedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        Guid? mediaId = null;
        string? url = null;

        if (domainEvent.AvatarStorageKey is not null)
        {
            if (!AvatarKeys.TryGetMediaId(domainEvent.AvatarStorageKey, out var id))
            {
                return;
            }

            mediaId = id;
            url = _avatars.Resolve(domainEvent.AvatarStorageKey);
        }

        await _integrationEvents.PublishAsync(new UserAvatarChangedIntegrationEvent
        {
            UserId = domainEvent.UserId.Value,
            MediaId = mediaId,
            AvatarUrl = url
        }, cancellationToken);
    }
}
