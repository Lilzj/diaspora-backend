using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Application.Services;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;
using Diaspora.Modules.Community.Observability;

namespace Diaspora.Modules.Community.Application.EventHandlers;

/// <summary>
/// Publishes the event Search indexes on and Recommendations learns from.
/// <para>
/// The hashtags are loaded here rather than carried on the domain event, because the domain
/// event is raised inside the aggregate before the hashtag pass has run. Publishing the tags
/// from the saved post is the only way the integration event carries the ones that were actually
/// stored.
/// </para>
/// </summary>
public sealed class PostPublishedEventHandler : DomainEventHandler<PostPublishedDomainEvent>
{
    private readonly CommunityIntegrationEventRelay _relay;
    private readonly ICommunityPostRepository _posts;

    public PostPublishedEventHandler(
        CommunityIntegrationEventRelay relay, ICommunityPostRepository posts)
    {
        _relay = relay;
        _posts = posts;
    }

    protected override async Task HandleAsync(
        PostPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        CommunityTelemetry.PostsPublished.Add(
            1,
            new KeyValuePair<string, object?>(
                "category", CommunityWireNames.ToWire(domainEvent.Category)),
            new KeyValuePair<string, object?>(
                "urgency", CommunityWireNames.ToWire(domainEvent.Urgency)));

        var post = await _posts.GetForReadAsync(domainEvent.PostId, cancellationToken);

        await _relay.PublishAsync(
            new CommunityPostPublishedIntegrationEvent
            {
                PostId = domainEvent.PostId.Value,
                AuthorId = domainEvent.AuthorId.Value,
                OrganizationId = domainEvent.OrganizationId,
                Category = CommunityWireNames.ToWire(domainEvent.Category),
                Urgency = CommunityWireNames.ToWire(domainEvent.Urgency),
                Visibility = CommunityWireNames.ToWire(domainEvent.Visibility),
                CountryCode = domainEvent.CountryCode,
                AreaId = domainEvent.AreaId,
                Hashtags = post?.Hashtags.Select(hashtag => hashtag.Slug).ToList() ?? [],
                PublishedAt = post?.PublishedAt ?? domainEvent.OccurredAt
            },
            cancellationToken);
    }
}
