using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>The member filed a message. The counters follow from the archived domain event.</summary>
public sealed class ArchiveNotificationCommandHandler
    : ICommandHandler<ArchiveNotificationCommand, NotificationResponse>
{
    private readonly NotificationMutationService _mutations;
    private readonly IClock _clock;

    public ArchiveNotificationCommandHandler(NotificationMutationService mutations, IClock clock)
    {
        _mutations = mutations;
        _clock = clock;
    }

    public Task<Result<NotificationResponse>> Handle(
        ArchiveNotificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        return _mutations.ApplyAsync(
            command.NotificationId,
            (notification, _) => notification.Archive(
                ArchiveReason.Archived, byRecipient: true, _clock.UtcNow),
            cancellationToken);
    }
}
