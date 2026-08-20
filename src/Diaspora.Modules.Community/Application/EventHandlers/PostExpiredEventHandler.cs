using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Posts;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// Publishes an expiry.
/// <para>
/// Distinguished from a delete by its reason, because a consumer should treat the two
/// differently: an expired post keeps its permalink and its thread, so an index that drops it
/// makes an existing link 404.
/// </para>
/// </summary>
public sealed class PostExpiredEventHandler : DomainEventHandler<PostExpiredDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;
    private readonly ICommunityPostRepository _posts;

    public PostExpiredEventHandler(
        CommunityIntegrationEventRelay relay, ICommunityPostRepository posts)
    {
        _relay = relay;
        _posts = posts;
    }

    protected override async Task HandleAsync(
        PostExpiredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        var post = await _posts.GetForReadAsync(domainEvent.PostId, cancellationToken);

        await _relay.PublishAsync(
            new CommunityPostRemovedIntegrationEvent
            {
                PostId = domainEvent.PostId.Value,
                AuthorId = post?.AuthorId.Value ?? Guid.Empty,
                Reason = "expired"
            },
            cancellationToken);
    }
}
