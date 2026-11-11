using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Preferences;

/// <summary>
/// Everything the settings screen renders.
/// <para>
/// Serves both of the brief's "notification preferences" and "communication preferences" from one
/// record, because they are one screen and are always saved together. The client reads whichever
/// half it needs.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.PreferencesRead)]
public sealed record GetNotificationPreferencesQuery : IQuery<NotificationPreferencesResponse>;
