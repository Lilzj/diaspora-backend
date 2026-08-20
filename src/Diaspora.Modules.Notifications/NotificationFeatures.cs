namespace Diaspora.Modules.Notifications;

/// <summary>
/// The feature gates the Communication Platform owns.
/// <para>
/// <b>The inbox is not gated, and that is a deliberate refusal.</b> Every other vertical gates its
/// entry point at <see cref="Contracts.Authorization.AccessTier.Limited"/> or above. This one must
/// not: the first message a brand-new account receives is the one telling it what to do next, and
/// the second is the security alert. A gate on reading your own inbox would silence the platform
/// for exactly the people who most need to hear from it.
/// </para>
/// <para>
/// What <em>is</em> gated is everything that costs somebody else something: registering a delivery
/// endpoint (a push token is a write into a provider's queue), and authoring templates (a template
/// is a message the platform will send in its own name, to anyone).
/// </para>
/// </summary>
public static class NotificationFeatures
{
    /// <summary>Read your own inbox and preferences. Ungated on purpose — see the class summary.</summary>
    public const string Inbox = "notifications.inbox";

    /// <summary>Register a device or endpoint for push delivery.</summary>
    public const string Devices = "notifications.devices";

    /// <summary>Receive and generate digests.</summary>
    public const string Digests = "notifications.digests";

    /// <summary>Author, publish and retire notification templates. Staff only.</summary>
    public const string Templates = "notifications.templates";

    /// <summary>Read delivery history and analytics across members. Staff only.</summary>
    public const string Analytics = "notifications.analytics";

    /// <summary>Retry a failed delivery and work the failure queue. Staff only.</summary>
    public const string Operations = "notifications.operations";
}
