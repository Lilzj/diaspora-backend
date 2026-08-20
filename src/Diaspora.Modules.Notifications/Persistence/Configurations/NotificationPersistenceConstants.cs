namespace Diaspora.Modules.Notifications.Persistence.Configurations;

/// <summary>
/// Table names and column widths, in one place.
/// <para>
/// Declared once rather than inline for the reason every module since B5 records: a name repeated in
/// two configurations is a name that will differ in one of them, and the difference shows up as a
/// migration that drops a table.
/// </para>
/// </summary>
internal static class NotificationPersistenceConstants
{
    public const string NotificationsTable = "notifications";
    public const string DeliveriesTable = "notification_deliveries";
    public const string AttemptsTable = "notification_delivery_attempts";
    public const string ActionsTable = "notification_actions";
    public const string InboxesTable = "user_inboxes";
    public const string InboxCountersTable = "user_inbox_counters";
    public const string PreferencesTable = "notification_preferences";
    public const string ChannelPreferencesTable = "notification_channel_preferences";
    public const string CategoryPreferencesTable = "notification_category_preferences";
    public const string TemplatesTable = "notification_templates";
    public const string LocalizationsTable = "notification_template_localizations";
    public const string VariantsTable = "notification_template_variants";
    public const string VariablesTable = "notification_template_variables";
    public const string TemplateActionsTable = "notification_template_actions";
    public const string DigestsTable = "notification_digests";
    public const string DigestEntriesTable = "notification_digest_entries";
    public const string CategoriesTable = "notification_categories";
    public const string RulesTable = "notification_rules";
    public const string DevicesTable = "notification_devices";
    public const string AnalyticsTable = "notification_analytics";

    /// <summary>Enum names are stored as text; nothing in this module has a longer one.</summary>
    public const int EnumLength = 32;

    public const int KeyLength = 128;
    public const int LanguageLength = 16;
    public const int TimeZoneLength = 64;
    public const int TitleLength = 256;
    public const int BodyLength = 2048;
    public const int ShortBodyLength = 256;
    public const int RouteLength = 512;
    public const int ReasonLength = 256;
    public const int TokenLength = 512;
    public const int PlatformLength = 32;
}
