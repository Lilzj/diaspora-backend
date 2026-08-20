namespace Diaspora.Modules.Notifications;

/// <summary>
/// The stable machine-readable failure codes this module returns.
/// <para>
/// Prefixed with the module name so a client can switch on the code without parsing the message,
/// and versioned with the API rather than with the code that raises them.
/// </para>
/// </summary>
public static class NotificationErrorCodes
{
    public const string NotificationNotFound = "Notifications.Notification.NotFound";
    public const string NotificationNotOwned = "Notifications.Notification.NotOwned";
    public const string NotificationAlreadyArchived = "Notifications.Notification.AlreadyArchived";
    public const string NotificationExpired = "Notifications.Notification.Expired";
    public const string NotificationTransitionInvalid = "Notifications.Notification.TransitionInvalid";

    public const string InboxNotFound = "Notifications.Inbox.NotFound";
    public const string PinLimitReached = "Notifications.Inbox.PinLimitReached";

    public const string PreferenceNotFound = "Notifications.Preference.NotFound";
    public const string PreferenceInvalid = "Notifications.Preference.Invalid";
    public const string QuietHoursInvalid = "Notifications.Preference.QuietHoursInvalid";
    public const string LanguageUnsupported = "Notifications.Preference.LanguageUnsupported";
    public const string TimeZoneUnknown = "Notifications.Preference.TimeZoneUnknown";
    public const string CriticalChannelRequired = "Notifications.Preference.CriticalChannelRequired";

    public const string TemplateNotFound = "Notifications.Template.NotFound";
    public const string TemplateKeyTaken = "Notifications.Template.KeyTaken";
    public const string TemplateNotDraft = "Notifications.Template.NotDraft";
    public const string TemplateNotPublished = "Notifications.Template.NotPublished";
    public const string TemplateInvalid = "Notifications.Template.Invalid";
    public const string TemplateVariableUnknown = "Notifications.Template.VariableUnknown";
    public const string TemplateLocaleMissing = "Notifications.Template.LocaleMissing";

    public const string CategoryNotFound = "Notifications.Category.NotFound";
    public const string CategoryMandatory = "Notifications.Category.Mandatory";
    public const string UnsubscribeTokenInvalid = "Notifications.Unsubscribe.TokenInvalid";
    public const string RuleNotFound = "Notifications.Rule.NotFound";

    public const string DeviceNotFound = "Notifications.Device.NotFound";
    public const string DeviceLimitReached = "Notifications.Device.LimitReached";
    public const string DeviceTokenInvalid = "Notifications.Device.TokenInvalid";
    public const string DeviceSessionUnknown = "Notifications.Device.SessionUnknown";

    public const string DigestNotFound = "Notifications.Digest.NotFound";
    public const string DigestEmpty = "Notifications.Digest.Empty";
    public const string DigestAlreadyGenerated = "Notifications.Digest.AlreadyGenerated";

    public const string DeliveryNotFound = "Notifications.Delivery.NotFound";
    public const string DeliveryNotRetryable = "Notifications.Delivery.NotRetryable";
    public const string ChannelUnavailable = "Notifications.Delivery.ChannelUnavailable";

    public const string DeepLinkInvalid = "Notifications.DeepLink.Invalid";

    public const string Unauthenticated = "Notifications.Auth.Unauthenticated";
    public const string Forbidden = "Notifications.Auth.Forbidden";
    public const string RateLimited = "Notifications.RateLimit.Exceeded";
}
