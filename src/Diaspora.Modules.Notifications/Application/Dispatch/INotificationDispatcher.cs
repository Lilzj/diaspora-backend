using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Application.Dispatch;

/// <summary>
/// The single door through which every notification in the platform is created.
/// <para>
/// <b>There is no other way to make one.</b> No command creates a notification, no module can
/// construct the aggregate from outside, and the only public entry point is a trigger describing a
/// published fact. That is what makes the success criterion — business modules publish events and
/// never create notifications — a structural property rather than a convention.
/// </para>
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>
    /// Matches rules, renders, applies preferences, and queues whatever survives.
    /// <para>
    /// Returns success with an empty result when no rule matches or every channel is suppressed.
    /// That is an ordinary outcome — a member who muted a category has been correctly served by
    /// being told nothing — and failing would put a consumed integration event into a retry loop
    /// over a preference.
    /// </para>
    /// </summary>
    Task<Result<NotificationDispatchResult>> DispatchAsync(
        NotificationTrigger trigger, CancellationToken cancellationToken = default);
}
