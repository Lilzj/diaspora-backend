using Diaspora.Application.Abstractions;
using Diaspora.Contracts.Community.V1.Events;
using Diaspora.Modules.Community.Domain.Enums;
using Diaspora.Modules.Community.Domain.Events;
using Diaspora.Modules.Community.Domain.Identifiers;
using Diaspora.Modules.Community.Domain.Posts;

namespace Diaspora.Modules.Community.Application.EventHandlers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The live alert map's half of the post lifecycle — 16 Sep 2026.
//
// A map open on a phone used to learn about a new, withdrawn or amended alert only when it next
// refetched on its own. Each handler below turns one lifecycle event into a
// CommunityAlertChangedIntegrationEvent; Notifications turns that into a signal on the map cells the
// alert touches, and a phone watching one of those cells refetches.
//
// One rule for all of them, kept in AlertMapAnnouncer: only a post a public map shows is announced —
// public, with coordinates — which is the rule the nearby route uses to decide what to draw. A
// private post announced to a public map cell would tell everybody watching it that something was
// posted there, even with no content in the frame.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Loads the saved post and publishes the map signal if a public map shows it.</summary>
public sealed class AlertMapAnnouncer
{
    private readonly CommunityIntegrationEventRelay _relay;
    private readonly ICommunityPostRepository _posts;

    public AlertMapAnnouncer(CommunityIntegrationEventRelay relay, ICommunityPostRepository posts)
    {
        _relay = relay;
        _posts = posts;
    }

    public async Task AnnounceAsync(
        CommunityPostId postId,
        string changeKind,
        string? reason,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        var post = await _posts.GetForReadAsync(postId, cancellationToken);

        if (post is null
            || !post.Audience.IsPublic
            || post.Location.Latitude is not { } latitude
            || post.Location.Longitude is not { } longitude)
        {
            return;
        }

        await _relay.PublishAsync(
            new CommunityAlertChangedIntegrationEvent
            {
                PostId = postId.Value,
                ChangeKind = changeKind,
                Reason = reason,
                Latitude = latitude,
                Longitude = longitude,
                AffectedRadiusMetres = post.AffectedRadiusMetres,
                ChangedAt = changedAt
            },
            cancellationToken);
    }
}

/// <summary>The realtime seam's change kinds, as the map signal carries them.</summary>
internal static class AlertChangeKinds
{
    public const string Created = "created";

    public const string Updated = "updated";

    public const string Cancelled = "cancelled";

    public const string StatusChanged = "status.changed";

    public const string Removed = "removed";
}

public sealed class AlertPublishedMapHandler : DomainEventHandler<PostPublishedDomainEvent>
{
    private readonly AlertMapAnnouncer _announcer;

    public AlertPublishedMapHandler(AlertMapAnnouncer announcer) => _announcer = announcer;

    protected override Task HandleAsync(PostPublishedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _announcer.AnnounceAsync(
            domainEvent.PostId, AlertChangeKinds.Created, null, domainEvent.OccurredAt, cancellationToken);
    }
}

public sealed class AlertAmendedMapHandler : DomainEventHandler<PostAlertAmendedDomainEvent>
{
    private readonly AlertMapAnnouncer _announcer;

    public AlertAmendedMapHandler(AlertMapAnnouncer announcer) => _announcer = announcer;

    protected override Task HandleAsync(PostAlertAmendedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _announcer.AnnounceAsync(
            domainEvent.PostId, AlertChangeKinds.Updated, "amended", domainEvent.AmendedAt, cancellationToken);
    }
}

/// <summary>A changed body changes the card a map shows, so the map is told too.</summary>
public sealed class AlertEditedMapHandler : DomainEventHandler<PostEditedDomainEvent>
{
    private readonly AlertMapAnnouncer _announcer;

    public AlertEditedMapHandler(AlertMapAnnouncer announcer) => _announcer = announcer;

    protected override Task HandleAsync(PostEditedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _announcer.AnnounceAsync(
            domainEvent.PostId, AlertChangeKinds.Updated, "edited", domainEvent.OccurredAt, cancellationToken);
    }
}

public sealed class AlertWithdrawnMapHandler : DomainEventHandler<PostWithdrawnDomainEvent>
{
    private readonly AlertMapAnnouncer _announcer;

    public AlertWithdrawnMapHandler(AlertMapAnnouncer announcer) => _announcer = announcer;

    protected override Task HandleAsync(PostWithdrawnDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _announcer.AnnounceAsync(
            domainEvent.PostId, AlertChangeKinds.StatusChanged, "withdrawn", domainEvent.ClosedAt, cancellationToken);
    }
}

public sealed class AlertCancelledMapHandler : DomainEventHandler<PostCancelledDomainEvent>
{
    private readonly AlertMapAnnouncer _announcer;

    public AlertCancelledMapHandler(AlertMapAnnouncer announcer) => _announcer = announcer;

    protected override Task HandleAsync(PostCancelledDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _announcer.AnnounceAsync(
            domainEvent.PostId, AlertChangeKinds.Cancelled, "author", domainEvent.ClosedAt, cancellationToken);
    }
}

public sealed class AlertExpiredMapHandler : DomainEventHandler<PostExpiredDomainEvent>
{
    private readonly AlertMapAnnouncer _announcer;

    public AlertExpiredMapHandler(AlertMapAnnouncer announcer) => _announcer = announcer;

    protected override Task HandleAsync(PostExpiredDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _announcer.AnnounceAsync(
            domainEvent.PostId, AlertChangeKinds.StatusChanged, "expired", domainEvent.ExpiredAt, cancellationToken);
    }
}

public sealed class AlertDeletedMapHandler : DomainEventHandler<PostDeletedDomainEvent>
{
    private readonly AlertMapAnnouncer _announcer;

    public AlertDeletedMapHandler(AlertMapAnnouncer announcer) => _announcer = announcer;

    protected override Task HandleAsync(PostDeletedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        return _announcer.AnnounceAsync(
            domainEvent.PostId,
            AlertChangeKinds.Removed,
            domainEvent.ByModerator ? "moderation" : "author",
            domainEvent.OccurredAt,
            cancellationToken);
    }
}

/// <summary>
/// Hiding an alert takes it off the map as surely as removing it, and restoring one puts it back —
/// neither raises a post event of its own, so the decision is read here.
/// </summary>
public sealed class AlertModeratedMapHandler : DomainEventHandler<ModerationDecidedDomainEvent>
{
    private readonly AlertMapAnnouncer _announcer;

    public AlertModeratedMapHandler(AlertMapAnnouncer announcer) => _announcer = announcer;

    protected override Task HandleAsync(ModerationDecidedDomainEvent domainEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);

        if (domainEvent.Content.Type != CommunityContentType.Post)
        {
            return Task.CompletedTask;
        }

        var kind = domainEvent.Action switch
        {
            ModerationActionType.Hide or ModerationActionType.Archive => AlertChangeKinds.Removed,
            ModerationActionType.Restore or ModerationActionType.Approve => AlertChangeKinds.Updated,
            _ => null
        };

        return kind is null
            ? Task.CompletedTask
            : _announcer.AnnounceAsync(
                CommunityPostId.From(domainEvent.Content.Id),
                kind,
                "moderation",
                domainEvent.OccurredAt,
                cancellationToken);
    }
}
