using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// Everything read at once.
/// <para>
/// <b>The counter is set to zero rather than decremented per message, and that is deliberate.</b>
/// The rows and the counter are updated by separate statements here, so deriving the new value from
/// the old one would carry any existing drift forward forever. Setting it is also the only correct
/// answer when the batch cap means some messages were left unread — the recount at the end reports
/// what is actually true.
/// </para>
/// </summary>
public sealed class MarkAllNotificationsReadCommandHandler
    : ICommandHandler<MarkAllNotificationsReadCommand, UnreadCountResponse>
{
    /// <summary>Messages one call will mark. A member past this makes a second call.</summary>
    private const int BatchSize = 500;

    private readonly INotificationRepository _notifications;
    private readonly IUserInboxRepository _inboxes;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public MarkAllNotificationsReadCommandHandler(
        INotificationRepository notifications,
        IUserInboxRepository inboxes,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _notifications = notifications;
        _inboxes = inboxes;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<UnreadCountResponse>> Handle(
        MarkAllNotificationsReadCommand command, CancellationToken cancellationToken)
    {
        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<UnreadCountResponse>(caller.Error);
        }

        var unread = await _notifications.ListUnreadAsync(caller.Value, BatchSize, cancellationToken);
        var now = _clock.UtcNow;

        foreach (var notification in unread)
        {
            notification.MarkRead(NotificationChannelKind.InApp.ToString(), now);
            _notifications.Update(notification);
        }

        var inbox = await _inboxes.GetOrCreateAsync(caller.Value, cancellationToken);

        // Every message just read raises an opened event whose handler decrements the badge. Setting
        // it to zero afterwards would be redundant for a member inside the batch cap and wrong for
        // one past it, so the remaining count is measured instead.
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        var remaining = await _notifications.CountUnreadAsync(caller.Value, cancellationToken);

        return Result.Success(new UnreadCountResponse
        {
            Total = remaining,
            ByCategory = inbox.Categories.ToDictionary(
                counter => counter.CategoryKey, counter => counter.UnreadCount, StringComparer.Ordinal),
            HasNew = false,
            LastNotificationAt = inbox.LastNotificationAt
        });
    }
}
