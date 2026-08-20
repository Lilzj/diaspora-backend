using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Notifications.Domain.Errors;

/// <summary>
/// Every failure this module can return, named once.
/// <para>
/// Declared as static readonly fields rather than built at the call site so two handlers cannot
/// return the same failure with two different descriptions — which is how a client ends up
/// switching on message text.
/// </para>
/// </summary>
public static class NotificationErrors
{
    // ── The message ─────────────────────────────────────────────────────────────────────────

    public static readonly Error NotificationNotFound = Error.NotFound(
        NotificationErrorCodes.NotificationNotFound,
        "That notification does not exist.");

    /// <summary>
    /// Deliberately identical in wording to <see cref="NotificationNotFound"/> and different in
    /// code. A member probing identifiers must not be able to learn that a message exists but
    /// belongs to somebody else; an operator reading logs must be able to tell the two apart.
    /// </summary>
    public static readonly Error NotificationNotOwned = Error.NotFound(
        NotificationErrorCodes.NotificationNotOwned,
        "That notification does not exist.");

    public static readonly Error NotificationAlreadyArchived = Error.Conflict(
        NotificationErrorCodes.NotificationAlreadyArchived,
        "That notification is already archived.");

    public static readonly Error NotificationExpired = Error.Conflict(
        NotificationErrorCodes.NotificationExpired,
        "That notification has expired and can no longer be changed.");

    public static readonly Error NotificationTransitionInvalid = Error.Conflict(
        NotificationErrorCodes.NotificationTransitionInvalid,
        "That notification cannot move to the requested state.");

    // ── The inbox ───────────────────────────────────────────────────────────────────────────

    public static readonly Error InboxNotFound = Error.NotFound(
        NotificationErrorCodes.InboxNotFound,
        "No inbox exists for that member yet.");

    public static readonly Error PinLimitReached = Error.Conflict(
        NotificationErrorCodes.PinLimitReached,
        "You have pinned as many notifications as an inbox can usefully hold. Unpin one first.");

    // ── Preferences ─────────────────────────────────────────────────────────────────────────

    public static readonly Error PreferenceNotFound = Error.NotFound(
        NotificationErrorCodes.PreferenceNotFound,
        "No communication preferences exist for that member.");

    public static readonly Error PreferenceInvalid = Error.Validation(
        NotificationErrorCodes.PreferenceInvalid,
        "Those preferences are not valid.");

    public static readonly Error QuietHoursInvalid = Error.Validation(
        NotificationErrorCodes.QuietHoursInvalid,
        "Quiet hours must be whole hours between 0 and 23, and must not start and end at the same hour.");

    public static readonly Error LanguageUnsupported = Error.Validation(
        NotificationErrorCodes.LanguageUnsupported,
        "That is not a recognisable language tag.");

    public static readonly Error TimeZoneUnknown = Error.Validation(
        NotificationErrorCodes.TimeZoneUnknown,
        "That is not a time zone this platform knows.");

    public static readonly Error CriticalChannelRequired = Error.Validation(
        NotificationErrorCodes.CriticalChannelRequired,
        "Security notifications cannot be switched off. They are always delivered to your inbox.");

    // ── Templates ───────────────────────────────────────────────────────────────────────────

    public static readonly Error TemplateNotFound = Error.NotFound(
        NotificationErrorCodes.TemplateNotFound,
        "That template does not exist.");

    public static readonly Error TemplateKeyTaken = Error.Conflict(
        NotificationErrorCodes.TemplateKeyTaken,
        "A template with that key already exists.");

    public static readonly Error TemplateNotDraft = Error.Conflict(
        NotificationErrorCodes.TemplateNotDraft,
        "Only a draft template can be edited. Create a new version instead.");

    public static readonly Error TemplateNotPublished = Error.Conflict(
        NotificationErrorCodes.TemplateNotPublished,
        "That template has not been published and cannot be used to send anything.");

    public static readonly Error TemplateInvalid = Error.Validation(
        NotificationErrorCodes.TemplateInvalid,
        "That template is not valid.");

    public static readonly Error TemplateVariableUnknown = Error.Validation(
        NotificationErrorCodes.TemplateVariableUnknown,
        "The template references a variable it does not declare.");

    public static readonly Error TemplateLocaleMissing = Error.Validation(
        NotificationErrorCodes.TemplateLocaleMissing,
        "A template must carry at least its default language before it can be published.");

    // ── Catalogue ───────────────────────────────────────────────────────────────────────────

    public static readonly Error CategoryNotFound = Error.NotFound(
        NotificationErrorCodes.CategoryNotFound,
        "That notification category does not exist.");

    public static readonly Error RuleNotFound = Error.NotFound(
        NotificationErrorCodes.RuleNotFound,
        "No routing rule matches that event.");

    /// <summary>
    /// An attempt to switch off a category the catalogue says nobody may switch off — B13.3.
    /// <para>
    /// Refused rather than silently ignored. A member who followed an unsubscribe link and was shown
    /// a success page while nothing changed would follow it again, and then complain that the
    /// platform ignores unsubscribes — which by then would be a reasonable thing to believe.
    /// </para>
    /// </summary>
    public static readonly Error CategoryMandatory = Error.Conflict(
        NotificationErrorCodes.CategoryMandatory,
        "This category carries security and account messages and cannot be switched off.");

    /// <summary>
    /// An unsubscribe link that does not verify — B13.3.
    /// <para>
    /// <b>One error for expired, forged, truncated and replayed-with-an-edit.</b> Distinguishing
    /// them would tell whoever is holding the link which part they got wrong, and the link is the
    /// only credential on the platform that arrives without a session behind it.
    /// </para>
    /// </summary>
    public static readonly Error UnsubscribeTokenInvalid = Error.Validation(
        NotificationErrorCodes.UnsubscribeTokenInvalid,
        "That unsubscribe link is not valid or has expired. You can change this in your settings.");

    // ── Endpoints ───────────────────────────────────────────────────────────────────────────

    public static readonly Error DeviceNotFound = Error.NotFound(
        NotificationErrorCodes.DeviceNotFound,
        "That device is not registered for notifications.");

    public static readonly Error DeviceLimitReached = Error.Conflict(
        NotificationErrorCodes.DeviceLimitReached,
        "You have registered as many devices as an account may hold. Remove one first.");

    public static readonly Error DeviceTokenInvalid = Error.Validation(
        NotificationErrorCodes.DeviceTokenInvalid,
        "That push token is not in a usable form.");

    /// <summary>
    /// "Sign my other devices out" asked by a caller whose token names no device.
    /// <para>
    /// Refused rather than interpreted. The operation is "all except this one", and a server that
    /// cannot tell which one "this" is would either retire nothing — a silent no-op the member reads
    /// as done — or retire everything, including the phone in their hand.
    /// </para>
    /// </summary>
    public static readonly Error DeviceSessionUnknown = Error.Validation(
        NotificationErrorCodes.DeviceSessionUnknown,
        "This session is not associated with a device, so the others cannot be signed out from here.");

    // ── Digests ─────────────────────────────────────────────────────────────────────────────

    public static readonly Error DigestNotFound = Error.NotFound(
        NotificationErrorCodes.DigestNotFound,
        "That digest does not exist.");

    public static readonly Error DigestEmpty = Error.Conflict(
        NotificationErrorCodes.DigestEmpty,
        "There was nothing to summarise for that window.");

    public static readonly Error DigestAlreadyGenerated = Error.Conflict(
        NotificationErrorCodes.DigestAlreadyGenerated,
        "A digest has already been generated for that window.");

    // ── Delivery ────────────────────────────────────────────────────────────────────────────

    public static readonly Error DeliveryNotFound = Error.NotFound(
        NotificationErrorCodes.DeliveryNotFound,
        "That delivery record does not exist.");

    public static readonly Error DeliveryNotRetryable = Error.Conflict(
        NotificationErrorCodes.DeliveryNotRetryable,
        "That delivery cannot be retried. Suppressed and delivered messages are not resent.");

    public static readonly Error ChannelUnavailable = Error.ServiceUnavailable(
        NotificationErrorCodes.ChannelUnavailable,
        "No transport is configured for that channel.");

    public static readonly Error DeepLinkInvalid = Error.Validation(
        NotificationErrorCodes.DeepLinkInvalid,
        "That deep link does not name a route this platform can open.");

    // ── Access ──────────────────────────────────────────────────────────────────────────────

    public static readonly Error Unauthenticated = Error.Unauthorized(
        NotificationErrorCodes.Unauthenticated,
        "Sign in to read your notifications.");

    public static readonly Error Forbidden = Error.Forbidden(
        NotificationErrorCodes.Forbidden,
        "You cannot do that.");

    public static readonly Error RateLimited = Error.RateLimited(
        NotificationErrorCodes.RateLimited,
        "Too many requests. Try again shortly.");
}
