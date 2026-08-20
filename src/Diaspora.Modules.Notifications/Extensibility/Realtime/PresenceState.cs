namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// Whether a member is reachable on a live connection right now.
/// <para>
/// <b>Three states rather than a boolean, because "away" is the one that stops presence from
/// lying.</b> A phone in a pocket holds its socket open for minutes after the person stopped
/// looking at it; reporting that as online teaches members that the green dot means nothing. Away
/// is "connected, and idle for longer than the deployment's patience".
/// </para>
/// <para>
/// The wire spelling is lower case — <c>online</c>, <c>away</c>, <c>offline</c> — and is produced by
/// <see cref="PresenceSnapshot.Status"/> rather than by serialising this enum, so a rename here can
/// never silently change a published contract.
/// </para>
/// </summary>
public enum PresenceState
{
    /// <summary>No live connection. The default for everybody the registry has never seen.</summary>
    Offline = 0,

    /// <summary>Connected and recently active.</summary>
    Online = 1,

    /// <summary>Connected, but idle for longer than <c>Realtime:Presence:AwayAfterSeconds</c>.</summary>
    Away = 2
}
