namespace Diaspora.Modules.Notifications;

/// <summary>Names shared by the module's platform module and its contributors.</summary>
public static class NotificationsModuleMetadata
{
    public const string Name = "Notifications";

    public const string Version = "1.0.0";

    /// <summary>The module's own database schema. It owns these tables and no others.</summary>
    public const string Schema = "notifications";

    /// <summary>
    /// The one place the module's public URL space is declared, asserted by an architecture test.
    /// <para>
    /// <c>/notifications</c> rather than <c>/communication</c> or <c>/messages</c>. The mobile
    /// client already routes its inbox screen at <c>/notifications</c> and its settings screen at
    /// <c>/settings/notifications</c>, and <c>/messages</c> is the conversation surface — a URL
    /// space this module must not take, because real-time chat is explicitly somebody else's.
    /// </para>
    /// </summary>
    public const string RoutePrefix = "/api/v1/notifications";

    /// <summary>
    /// Where the real-time hub is mapped, declared once for the same reason the route prefix is.
    /// <para>
    /// <b>Outside the versioned API space on purpose.</b> A hub is not a resource and has no
    /// representation to version: its compatibility story is the event names in
    /// <c>RealtimeEvents</c> and the method names on the hub, both of which are additive and neither
    /// of which is expressed in a URL. Putting it under <c>/api/v1</c> would promise a <c>/api/v2</c>
    /// hub that could only ever be the same socket.
    /// </para>
    /// <para>
    /// <b><c>/hubs</c> is a platform-wide prefix rather than this module's.</b> The host accepts a
    /// bearer token from the query string for anything beneath it — the only way a browser or React
    /// Native WebSocket can authenticate, since neither can set a header on the upgrade — so a
    /// future module mapping a hub under the same prefix is authenticated with no host change. The
    /// prefix, not the module, is the thing the host knows about.
    /// </para>
    /// </summary>
    public const string RealtimeHubPath = "/hubs/realtime";
}
