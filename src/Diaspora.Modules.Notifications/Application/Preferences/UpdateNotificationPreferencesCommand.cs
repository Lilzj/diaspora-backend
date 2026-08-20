using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Requests;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Preferences;

/// <summary>
/// Changes how a member is contacted.
/// <para>
/// Every field on the request is optional, so a member toggling one switch sends one field. That is
/// what stops two clients on one screen from overwriting each other with stale copies of everything
/// they did not touch.
/// </para>
/// </summary>
[Authorize(Permission = NotificationPermissions.PreferencesManage)]
[Transactional(Reason = "Applies every section of the settings screen atomically.")]
[Auditable]
public sealed record UpdateNotificationPreferencesCommand : ICommand<NotificationPreferencesResponse>
{
    public required UpdateNotificationPreferencesRequest Request { get; init; }
}
