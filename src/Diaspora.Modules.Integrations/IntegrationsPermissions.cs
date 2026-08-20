namespace Diaspora.Modules.Integrations;

/// <summary>
/// The permissions the Integration Foundation owns.
/// <para>
/// Colon-separated <c>module:resource:action</c> keys, per the platform convention and enforced by
/// DSP5002 (policies use dots — see <see cref="IntegrationsPolicies"/>).
/// </para>
/// <para>
/// <b>Six, and the split is by blast radius rather than by screen.</b> Reading a connection tells
/// the caller that a member has a Google account; writing one puts a third-party credential into
/// this platform's custody; and writing a calendar entry changes something in a member's own life
/// that this platform cannot easily undo. Those are three different risks and a deployment should be
/// able to grant them separately.
/// </para>
/// </summary>
public static class IntegrationsPermissions
{
    private const string Prefix = "integrations";

    /// <summary>Read one's own connections, identity links, preferences and the capability report.</summary>
    public const string ConnectionsRead = Prefix + ":connections:read";

    /// <summary>Connect, reauthorize, revoke or disconnect one's own external account.</summary>
    public const string ConnectionsWrite = Prefix + ":connections:write";

    /// <summary>List the calendars a connected account exposes.</summary>
    public const string CalendarRead = Prefix + ":calendar:read";

    /// <summary>Write an entry into a connected calendar.</summary>
    public const string CalendarWrite = Prefix + ":calendar:write";

    /// <summary>Start a synchronisation and read its state.</summary>
    public const string CalendarSync = Prefix + ":calendar:sync";

    /// <summary>Change one's own integration preferences.</summary>
    public const string SettingsWrite = Prefix + ":settings:write";
}

/// <summary>
/// The policy names endpoints require.
/// <para>
/// Lower-case and dotted, namespaced under the module — the convention B1.4.1 settled and an
/// architecture test enforces. A policy name that is not namespaced can collide with another
/// module's, and an unregistered policy does not throw: ASP.NET Core denies the request and the log
/// says nothing about why.
/// </para>
/// </summary>
public static class IntegrationsPolicies
{
    private const string Prefix = "integrations.";

    public const string ConnectionsRead = Prefix + "connections.read";
    public const string ConnectionsWrite = Prefix + "connections.write";
    public const string CalendarRead = Prefix + "calendar.read";
    public const string CalendarWrite = Prefix + "calendar.write";
    public const string CalendarSync = Prefix + "calendar.sync";
    public const string SettingsWrite = Prefix + "settings.write";
}

/// <summary>The feature gates a deployment can switch.</summary>
public static class IntegrationsFeatures
{
    private const string Prefix = "integrations.";

    /// <summary>
    /// The connection centre and the OAuth exchange.
    /// <para>
    /// Separate from the calendar gate because a deployment may have an identity provider and no
    /// calendar one. Shipping a single gate would mean a deployment with neither either hides a
    /// centre it could serve or advertises a calendar it cannot write to.
    /// </para>
    /// </summary>
    public const string Connections = Prefix + "connections";

    /// <summary>The proxied calendar surfaces.</summary>
    public const string Calendar = Prefix + "calendar";
}
