using Diaspora.Modules.Notifications.Domain.Enums;

namespace Diaspora.Modules.Notifications.Domain.Policies;

/// <summary>
/// What each urgency level is allowed to do to a member's evening.
/// <para>
/// <b>This is the only place priority is interpreted, and that is the whole reason it exists.</b>
/// The same three questions — may it wake them, may a mute silence it, how hard do we retry — get
/// asked from the dispatcher, the quiet-hours check, the digest builder and the retry ladder. Four
/// copies of "if Critical" is four places to forget one.
/// </para>
/// </summary>
public static class NotificationPriorityPolicy
{
    /// <summary>
    /// Whether quiet hours and do-not-disturb are ignored.
    /// <para>
    /// Critical only. A member who has said "do not disturb me" has made a decision the platform
    /// respects for everything except the messages that exist because somebody may be attacking
    /// their account.
    /// </para>
    /// </summary>
    public static bool OverridesQuietHours(NotificationPriority priority) =>
        priority == NotificationPriority.Critical;

    /// <summary>
    /// Whether a category mute or a channel switch can silence it.
    /// <para>
    /// Critical cannot be muted, and the preference screen is required to say so rather than
    /// showing a toggle that does nothing.
    /// </para>
    /// </summary>
    public static bool CanBeSuppressedByPreference(NotificationPriority priority) =>
        priority != NotificationPriority.Critical;

    /// <summary>Whether the message may be folded into a digest instead of sent on its own.</summary>
    public static bool CanBeDigested(NotificationPriority priority) =>
        priority is NotificationPriority.Normal or NotificationPriority.Low;

    /// <summary>Whether any channel beyond the inbox is used at all.</summary>
    public static bool ReachesBeyondInbox(NotificationPriority priority) =>
        priority != NotificationPriority.Silent;

    /// <summary>
    /// How many attempts a failed delivery gets.
    /// <para>
    /// A fraud alert is worth eight tries; a reaction to a photograph is worth one. Retrying
    /// everything equally is how a provider outage turns a backlog of trivia into a queue that
    /// delays the messages that mattered.
    /// </para>
    /// </summary>
    public static int MaxAttempts(NotificationPriority priority) => priority switch
    {
        NotificationPriority.Critical => NotificationLimits.MaxDeliveryAttempts,
        NotificationPriority.High => 5,
        NotificationPriority.Normal => 3,
        NotificationPriority.Low => 1,
        _ => 0
    };

    /// <summary>How long to wait before attempt <paramref name="attempt"/>. Exponential, capped.</summary>
    public static TimeSpan BackoffFor(NotificationPriority priority, int attempt)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);

        var seed = priority == NotificationPriority.Critical ? 15 : 60;
        var seconds = Math.Min(seed * Math.Pow(2, Math.Max(0, attempt - 1)), 3600);

        return TimeSpan.FromSeconds(seconds);
    }
}
