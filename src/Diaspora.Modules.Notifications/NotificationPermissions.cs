namespace Diaspora.Modules.Notifications;

/// <summary>
/// The permissions the Communication Platform owns.
/// <para>
/// <b>Two bands, and the line between them is "whose message is it".</b> Everything a member does
/// to their own inbox — read, archive, pin, mute a category, set quiet hours, register a phone —
/// is theirs and is available at the lowest tier the platform has, including to a restricted
/// account. Silencing a suspended member's security alerts would be the platform withholding
/// exactly the information that account most needs.
/// </para>
/// <para>
/// Everything that speaks in the platform's own voice — authoring a template, retrying a delivery,
/// reading another member's delivery history — is staff, because a template is a message hundreds
/// of thousands of people will receive believing Diaspora wrote it.
/// </para>
/// </summary>
public static class NotificationPermissions
{
    // ── The member's own surface ────────────────────────────────────────────────────────────

    public const string InboxRead = "notifications:read";
    public const string InboxManage = "notifications:manage";
    public const string PreferencesRead = "notifications.preferences:read";
    public const string PreferencesManage = "notifications.preferences:manage";
    public const string DevicesManage = "notifications.devices:manage";
    public const string DigestsRead = "notifications.digests:read";
    public const string DigestsGenerate = "notifications.digests:generate";

    // ── The platform's own voice ────────────────────────────────────────────────────────────

    public const string TemplatesRead = "notifications.templates:read";
    public const string TemplatesManage = "notifications.templates:manage";
    public const string TemplatesPublish = "notifications.templates:publish";
    public const string DeliveryRead = "notifications.delivery:read";
    public const string DeliveryRetry = "notifications.delivery:retry";
    public const string AnalyticsRead = "notifications.analytics:read";
    public const string CatalogManage = "notifications.catalog:manage";
}
