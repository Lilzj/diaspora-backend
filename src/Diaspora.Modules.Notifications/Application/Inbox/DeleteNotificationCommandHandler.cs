using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Services;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Inbox;

/// <summary>
/// The member deleted a message.
/// <para>
/// <see cref="ISoftDeletable.MarkDeleted"/> is called explicitly rather than relying on
/// <c>Remove</c>, because the aggregate raises its deletion event from that method and the counters
/// depend on it. Letting the persistence interceptor do the stamping would settle the counters from
/// nothing.
/// </para>
/// </summary>
public sealed class DeleteNotificationCommandHandler : ICommandHandler<DeleteNotificationCommand>
{
    private readonly INotificationRepository _notifications;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public DeleteNotificationCommandHandler(
        INotificationRepository notifications,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork,
        ICurrentUser currentUser,
        IClock clock)
    {
        _notifications = notifications;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<Result> Handle(
        DeleteNotificationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure(caller.Error);
        }

        var loaded = await _notifications.GetAsync(
            NotificationId.From(command.NotificationId), cancellationToken);

        var owned = NotificationAuthorizationService.RequireOwnership(loaded, caller.Value);

        if (owned.IsFailure)
        {
            return Result.Failure(owned.Error);
        }

        owned.Value.MarkDeleted(_clock.UtcNow, _currentUser.UserId?.ToString());

        _notifications.Update(owned.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
