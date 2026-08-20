namespace Diaspora.Modules.Notifications.Domain.Enums;

/// <summary>
/// The transports a message can travel on.
/// <para>
/// <b>All seven exist here; three have implementations.</b> Declaring the future ones now is not
/// speculation — it is what keeps the preference record, the delivery table, the analytics rollup
/// and the wire contract from needing a migration the day SMS arrives. A channel with no
/// implementation simply never resolves an <c>INotificationChannel</c>, and the dispatcher records
/// the delivery as unsupported rather than pretending it went.
/// </para>
/// </summary>
public enum NotificationChannelKind
{
    /// <summary>The inbox. Always available, never fails for want of a provider, and the only channel a member cannot lose.</summary>
    InApp = 0,

    /// <summary>Device push. Implemented as a channel; the transport behind it is a port with no vendor.</summary>
    Push = 1,

    /// <summary>Email. Implemented as a channel; the transport behind it is a port with no vendor.</summary>
    Email = 2,

    /// <summary>Reserved. No channel implementation ships.</summary>
    Sms = 3,

    /// <summary>Reserved. No channel implementation ships.</summary>
    WhatsApp = 4,

    /// <summary>Reserved. No channel implementation ships.</summary>
    Voice = 5,

    /// <summary>Reserved. No channel implementation ships.</summary>
    Webhook = 6
}
