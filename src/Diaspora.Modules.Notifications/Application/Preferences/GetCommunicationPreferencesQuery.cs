using Diaspora.Application.Abstractions;
using Diaspora.Modules.Notifications.Contracts.V1.Responses;

namespace Diaspora.Modules.Notifications.Application.Preferences;

/// <summary>
/// The timing and language half of the settings screen.
/// <para>
/// The brief names "communication preferences" as its own thing, and a client may well render it on
/// its own screen. It is served from the same record as the channel switches, so the two cannot
/// drift — what is separate is the surface, not the state.
/// </para>
/// </summary>
[RequirePermission(NotificationPermissions.PreferencesRead)]
public sealed record GetCommunicationPreferencesQuery : IQuery<CommunicationPreferencesResponse>;
