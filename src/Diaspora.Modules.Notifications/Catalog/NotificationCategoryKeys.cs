namespace Diaspora.Modules.Notifications.Catalog;

/// <summary>
/// The category keys this module ships with.
/// <para>
/// <b>Constants for the seeds and the rules to agree on, not an enum.</b> The catalogue is a table
/// and a deployment may add rows; these are the ones the platform declares out of the box, named here
/// so a rule cannot reference a category nobody seeded.
/// </para>
/// <para>
/// Several match the existing mobile settings screen's storage keys exactly — <c>reviews</c>,
/// <c>updates</c>, <c>alerts</c>, <c>marketplace</c>, <c>guides</c>, <c>jobs</c>, <c>housing</c> — so
/// that screen can move from AsyncStorage to the server without remapping anything.
/// </para>
/// </summary>
public static class NotificationCategoryKeys
{
    /// <summary>Platform-wide announcements and account lifecycle.</summary>
    public const string Platform = "platform";

    /// <summary>Sign-ins, password changes, suspensions. Mandatory — cannot be muted.</summary>
    public const string Security = "security";

    /// <summary>Verification outcomes, badges, trust level changes.</summary>
    public const string Trust = "trust";

    /// <summary>The client's existing key.</summary>
    public const string Jobs = "jobs";

    /// <summary>The client's existing key.</summary>
    public const string Marketplace = "marketplace";

    /// <summary>The client's existing key.</summary>
    public const string Housing = "housing";

    /// <summary>Mentions, replies and reactions.</summary>
    public const string Community = "community";

    public const string Organizations = "organizations";

    public const string Events = "events";

    /// <summary>The client's existing key.</summary>
    public const string Guides = "guides";

    /// <summary>
    /// Something a member you follow did — today, publishing a guide. Its own key so following a
    /// contributor can be switched off without silencing every guide notification. 20 Oct 2026.
    /// </summary>
    public const string Following = "following";

    /// <summary>Learning paths and the country knowledge directory.</summary>
    public const string Learning = "learning";

    public const string Provider = "provider";

    /// <summary>Moderator decisions about a member's own content. Mandatory.</summary>
    public const string Moderation = "moderation";

    /// <summary>The client's existing key. Maintenance and release notes.</summary>
    public const string Updates = "updates";

    /// <summary>The client's existing key. Safety and area alerts.</summary>
    public const string Alerts = "alerts";

    /// <summary>The client's existing key. Reviews and recommendations received.</summary>
    public const string Reviews = "reviews";

    /// <summary>Reserved. Declared so a billing module lands without a client release.</summary>
    public const string Billing = "billing";
}
