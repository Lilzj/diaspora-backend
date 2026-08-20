using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Events;

namespace Diaspora.Modules.Notifications.Application.EventHandlers;

/// <summary>
/// A member deleted a message.
/// <para>
/// <b>Nothing is published.</b> A deletion is somebody asking for something to be gone, and
/// announcing it on a bus every module reads would be the exact opposite of honouring that. The
/// handler exists only so the counters stay right.
/// </para>
/// </summary>
public sealed class NotificationDeletedEventHandler
    : DomainEventHandler<NotificationDeletedDomainEvent>
{
    private readonly InboxCounterService _counters;

    public NotificationDeletedEventHandler(InboxCounterService counters) => _counters = counters;

    protected override Task HandleAsync(
        NotificationDeletedDomainEvent domainEvent, CancellationToken cancellationToken) =>
        _counters.RecordDeletedAsync(
            domainEvent.RecipientUserId,
            domainEvent.CategoryKey,
            domainEvent.WasUnread,
            cancellationToken);
}
