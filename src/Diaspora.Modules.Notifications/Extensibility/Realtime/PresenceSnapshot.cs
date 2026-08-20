using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Extensibility.Realtime;

/// <summary>
/// What the platform will say about one member's availability.
/// <para>
/// <b><see cref="Status"/> is a lower-case string rather than the enum, and that is the contract.</b>
/// The client renders <c>online</c>, <c>away</c> and <c>offline</c>; serialising
/// <see cref="PresenceState"/> would make a C# rename a silent breaking change on the wire, which is
/// the class of defect B13.1 recorded for integration events and the same rule applies here.
/// </para>
/// <para>
/// <b><see cref="LastSeenAt"/> is null while somebody is online, on purpose.</b> "Last seen" is a
/// statement about somebody who is not here; reporting it for somebody who is invites a client to
/// render "online · last seen 2 seconds ago", and a member who has never connected has no last-seen
/// to disclose at all.
/// </para>
/// </summary>
/// <param name="UserId">Who this is about.</param>
/// <param name="Status">Lower-case wire spelling: <c>online</c>, <c>away</c> or <c>offline</c>.</param>
/// <param name="LastSeenAt">When they were last connected. Null while connected, and null if never.</param>
/// <param name="ConnectionCount">
/// How many live sockets they hold. Zero for offline. Present because multi-device is the normal
/// case and "went offline" must mean the last one closed, not any one of them.
/// </param>
public sealed record PresenceSnapshot(
    UserId UserId,
    string Status,
    DateTimeOffset? LastSeenAt,
    int ConnectionCount)
{
    public const string Online = "online";

    public const string Away = "away";

    public const string Offline = "offline";

    /// <summary>The wire spelling for a state. The one place the mapping is written.</summary>
    public static string Spell(PresenceState state) => state switch
    {
        PresenceState.Online => Online,
        PresenceState.Away => Away,
        _ => Offline
    };

    /// <summary>Nobody is connected, and we may or may not know when they last were.</summary>
    public static PresenceSnapshot Absent(UserId userId, DateTimeOffset? lastSeenAt = null) =>
        new(userId, Offline, lastSeenAt, 0);
}
