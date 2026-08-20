using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Notifications.Application.Mapping;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;
using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Notifications;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Services;

/// <summary>
/// The shape every inbox command has: authorise the caller, load the message, check it is theirs,
/// change it, save, answer.
/// <para>
/// <b>One implementation of that shape rather than eleven copies.</b> Every step here is a step that
/// must not be skipped — and eleven handlers each writing the ownership check themselves is eleven
/// chances for one of them to load first and check second, or to save and forget to. The handlers
/// keep their own files and their own authorisation attributes; what they delegate is the sequence.
/// </para>
/// </summary>
public sealed class NotificationMutationService
{
    private readonly INotificationRepository _notifications;
    private readonly INotificationCategoryRepository _categories;
    private readonly NotificationAuthorizationService _authorization;
    private readonly IUnitOfWork _unitOfWork;

    public NotificationMutationService(
        INotificationRepository notifications,
        INotificationCategoryRepository categories,
        NotificationAuthorizationService authorization,
        IUnitOfWork unitOfWork)
    {
        _notifications = notifications;
        _categories = categories;
        _authorization = authorization;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Applies a change to one of the caller's own messages.
    /// </summary>
    /// <param name="notificationId">The message. Reported as not found when it is somebody else's.</param>
    /// <param name="mutate">The change. Runs only after ownership is established.</param>
    /// <param name="includeDeleted">
    /// Whether to load past the soft-delete filter. True only for the undelete path — the global
    /// query filter hides exactly the row a restore needs, which is the trap B1.0.1 recorded.
    /// </param>
    public async Task<Result<NotificationResponse>> ApplyAsync(
        Guid notificationId,
        Func<Notification, UserId, CancellationToken, Task<Result>> mutate,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        var caller = _authorization.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<NotificationResponse>(caller.Error);
        }

        var id = NotificationId.From(notificationId);

        var loaded = includeDeleted
            ? await _notifications.GetIncludingDeletedAsync(id, cancellationToken)
            : await _notifications.GetAsync(id, cancellationToken);

        var owned = NotificationAuthorizationService.RequireOwnership(loaded, caller.Value);

        if (owned.IsFailure)
        {
            return Result.Failure<NotificationResponse>(owned.Error);
        }

        var changed = await mutate(owned.Value, caller.Value, cancellationToken);

        if (changed.IsFailure)
        {
            return Result.Failure<NotificationResponse>(changed.Error);
        }

        _notifications.Update(owned.Value);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await MapAsync(owned.Value, cancellationToken));
    }

    /// <summary>The synchronous form, for the many mutations that touch only the aggregate.</summary>
    public Task<Result<NotificationResponse>> ApplyAsync(
        Guid notificationId,
        Func<Notification, UserId, Result> mutate,
        CancellationToken cancellationToken,
        bool includeDeleted = false)
    {
        ArgumentNullException.ThrowIfNull(mutate);

        return ApplyAsync(
            notificationId,
            (notification, userId, _) => Task.FromResult(mutate(notification, userId)),
            cancellationToken,
            includeDeleted);
    }

    /// <summary>The response for a message the caller already holds. Loads the catalogue once.</summary>
    public async Task<NotificationResponse> MapAsync(
        Notification notification, CancellationToken cancellationToken)
    {
        var categories = await _categories.ListActiveAsync(cancellationToken);

        return NotificationMapper.ToResponse(
            notification,
            categories.ToDictionary(category => category.Key, StringComparer.Ordinal));
    }
}
