using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member refused a message.
/// <para>
/// The aggregate marks it read as part of dismissing, which raises the opened event and settles the
/// badge. A dismissed message that still counted as unread would leave a badge nobody could clear
/// without un-dismissing it.
/// </para>
/// </summary>
public sealed class DismissNotificationCommandHandler
    : ICommandHandler<DismissNotificationCommand, NotificationResponse>
{
    private readonly NotificationMutationService _mutations;
    private readonly IClock _clock;

    public DismissNotificationCommandHandler(NotificationMutationService mutations, IClock clock)
    {
        _mutations = mutations;
        _clock = clock;
    }

    public Task<Result<NotificationResponse>> Handle(
        DismissNotificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _mutations.ApplyAsync(
            command.NotificationId,
            (notification, _) => notification.Dismiss(_clock.UtcNow),
            cancellationToken);
    }
}
