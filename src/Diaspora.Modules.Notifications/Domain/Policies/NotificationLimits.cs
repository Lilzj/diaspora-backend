namespace Diaspora.Modules.Notifications.Domain.Policies;

/// <summary>
/// The bounds the aggregates are correct at.
/// <para>
/// Constants rather than options, and the distinction is the one every vertical since B7 has drawn:
/// a bound an aggregate enforces as an invariant cannot vary by deployment, because an invariant
/// that varies is not an invariant. Abuse ceilings, sweep schedules and retention windows live in
/// <c>NotificationOptions</c> instead.
/// </para>
/// </summary>
public static class NotificationLimits
{
    /// <summary>A push title truncated by the OS is worse than one written to fit.</summary>
    public const int MaxTitleLength = 160;

    public const int MaxBodyLength = 2000;

    /// <summary>What a push payload can carry before providers start rejecting it.</summary>
    public const int MaxPushBodyLength = 240;

    public const int MaxActionLabelLength = 40;

    /// <summary>Three is what a notification card can show without becoming a menu.</summary>
    public const int MaxActions = 3;

    public const int MaxDeepLinkLength = 512;

    public const int MaxKeyLength = 128;

    public const int MaxTemplateVariables = 32;

    public const int MaxVariableNameLength = 64;

    public const int MaxVariableValueLength = 512;

    /// <summary>Locales one template may carry. Generous: the platform serves diasporas.</summary>
    public const int MaxLocalizations = 40;

    /// <summary>Entries one digest may hold before it stops being a summary.</summary>
    public const int MaxDigestEntries = 50;

    /// <summary>Delivery attempts recorded per channel. The ladder stops here whatever the options say.</summary>
    public const int MaxDeliveryAttempts = 8;

    /// <summary>Pinned messages one member may hold. A pinned inbox is an unpinned inbox.</summary>
    public const int MaxPinnedNotifications = 20;

    public const int MaxReasonLength = 256;

    public const int MaxSubjectIdLength = 128;

    /// <summary>BCP-47 tags are short; anything longer is not a language tag.</summary>
    public const int MaxLanguageTagLength = 16;

    public const int MaxTimeZoneIdLength = 64;
}
