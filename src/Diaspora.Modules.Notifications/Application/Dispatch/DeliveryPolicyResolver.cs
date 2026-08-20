using Diaspora.Modules.Notifications.Domain.Catalog;
using Diaspora.Modules.Notifications.Domain.Enums;
using Diaspora.Modules.Notifications.Domain.Policies;
using Diaspora.Modules.Notifications.Domain.Preferences;
using Diaspora.Modules.Notifications.Extensibility.Throttling;
using Diaspora.Modules.Notifications.Infrastructure.Channels;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Dispatch;

/// <summary>
/// Decides, for each channel, whether a message goes now, later, into a digest, or nowhere.
/// <para>
/// <b>The single most important class in the module, and the one place any of these questions is
/// answered.</b> Nine inputs — the rule, the category, the priority, the member's channel switch,
/// their category switch, their quiet hours, their do-not-disturb, their digest schedule and whether
/// a transport exists — collapse into one of five outcomes per channel. Spreading that over the
/// dispatcher, the channels and the digest builder is how a platform ends up honouring quiet hours
/// on push and ignoring them on email.
/// </para>
/// <para>
/// <b>The order of the checks is the policy.</b> Critical override first, because everything after
/// it is a member preference that Critical ignores. Then the rule's own suppression, because a rule
/// that says "record only" should not consult a preference at all. Then the member's switches, then
/// timing. Reordering any two of these changes what the platform does at three in the morning.
/// </para>
/// </summary>
public sealed class DeliveryPolicyResolver
{
    private readonly NotificationChannelRegistry _channels;
    private readonly INotificationThrottle _throttle;
    private readonly IClock _clock;

    public DeliveryPolicyResolver(
        NotificationChannelRegistry channels, INotificationThrottle throttle, IClock clock)
    {
        _channels = channels;
        _throttle = throttle;
        _clock = clock;
    }

    /// <summary>
    /// The plan for every channel the rule proposes, plus the inbox.
    /// <para>
    /// The inbox is always planned, whatever the rule says, because a message that exists nowhere a
    /// member can find it is a message that did not happen — and the audit trail is the reason this
    /// module keeps notifications at all.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<ChannelPlan>> ResolveAsync(
        UserId recipientUserId,
        NotificationRule rule,
        NotificationCategory? category,
        NotificationPreference? preference,
        NotificationPriority priority,
        DateTimeOffset? scheduledFor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(rule);

        var now = _clock.UtcNow;
        var plans = new List<ChannelPlan>(4);
        var proposed = Proposed(rule);

        foreach (var channel in proposed)
        {
            plans.Add(await ResolveChannelAsync(
                recipientUserId, channel, rule, category, preference, priority, scheduledFor, now,
                cancellationToken));
        }

        return plans;
    }

    /// <summary>
    /// The channels to consider: whatever the rule proposes, with the inbox always included.
    /// </summary>
    private static List<NotificationChannelKind> Proposed(NotificationRule rule)
    {
        var channels = rule.Channels.ToList();

        if (!channels.Contains(NotificationChannelKind.InApp))
        {
            channels.Insert(0, NotificationChannelKind.InApp);
        }

        return channels;
    }

    private async Task<ChannelPlan> ResolveChannelAsync(
        UserId recipientUserId,
        NotificationChannelKind channel,
        NotificationRule rule,
        NotificationCategory? category,
        NotificationPreference? preference,
        NotificationPriority priority,
        DateTimeOffset? scheduledFor,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // ── 1. Is there anything to send with? ──────────────────────────────────────────────
        // Asked first because it is a fact about the deployment rather than about the member, and
        // because recording Unsupported once is far better than failing eight times up a ladder.
        if (channel != NotificationChannelKind.InApp && !_channels.IsDeliverable(channel))
        {
            return ChannelPlan.Unsupported(channel, now);
        }

        var isCritical = !NotificationPriorityPolicy.CanBeSuppressedByPreference(priority);

        // ── 2. Silent never leaves the inbox ────────────────────────────────────────────────
        if (priority == NotificationPriority.Silent && channel != NotificationChannelKind.InApp)
        {
            return ChannelPlan.Suppress(channel, SuppressionReasons.SilentPriority, now);
        }

        // ── 3. The member's own switches ────────────────────────────────────────────────────
        // Skipped entirely for Critical, and skipped for the inbox, which cannot be switched off.
        if (!isCritical && channel != NotificationChannelKind.InApp && preference is not null)
        {
            if (preference.DoNotDisturb)
            {
                return ChannelPlan.Suppress(channel, SuppressionReasons.DoNotDisturb, now);
            }

            if (!preference.IsChannelEnabled(channel))
            {
                return ChannelPlan.Suppress(channel, SuppressionReasons.ChannelDisabled, now);
            }

            var categoryPreference = preference.CategoryFor(rule.CategoryKey);

            // A mandatory category ignores a mute. The preference screen renders its switch
            // disabled, and the aggregate refusing it here is what makes that honest rather than
            // cosmetic.
            if (category?.IsMandatory != true && !categoryPreference.Allows(channel))
            {
                return ChannelPlan.Suppress(channel, SuppressionReasons.CategoryMuted, now);
            }
        }

        // ── 4. The rule's own policy ────────────────────────────────────────────────────────
        if (rule.Policy == DeliveryPolicy.Suppressed && channel != NotificationChannelKind.InApp)
        {
            return ChannelPlan.Suppress(channel, SuppressionReasons.RulePolicy, now);
        }

        // ── 5. Digest ───────────────────────────────────────────────────────────────────────
        // Both the rule and the member can ask for one, and either is enough. Priority decides
        // whether it is allowed at all: nobody's daily summary should be where they learn their
        // account was compromised.
        if (channel != NotificationChannelKind.InApp
            && NotificationPriorityPolicy.CanBeDigested(priority)
            && WantsDigest(rule, category, preference))
        {
            return ChannelPlan.Digest(channel, now);
        }

        // ── 6. Throttling ───────────────────────────────────────────────────────────────────
        if (!await _throttle.TryAcquireAsync(
                recipientUserId, channel, priority, cancellationToken))
        {
            return ChannelPlan.Suppress(channel, SuppressionReasons.Throttled, now);
        }

        // ── 7. When ─────────────────────────────────────────────────────────────────────────
        var deliverAfter = scheduledFor is { } scheduled && scheduled > now ? scheduled : now;

        // Quiet hours defer rather than suppress. A message held until 07:00 still arrives; one
        // suppressed for being sent at 03:00 never does, and the member is not told either way.
        if (!NotificationPriorityPolicy.OverridesQuietHours(priority)
            && channel != NotificationChannelKind.InApp
            && preference is not null)
        {
            deliverAfter = ApplyQuietHours(preference, deliverAfter);
        }

        return deliverAfter > now
            ? ChannelPlan.Defer(channel, deliverAfter)
            : ChannelPlan.Send(channel, now);
    }

    /// <summary>
    /// Whether this message should wait for a digest.
    /// <para>
    /// Either the rule or the member asking is enough, and the category's default is the tie-break
    /// for a member who has never expressed an opinion. A member with digests switched off is never
    /// digested however the rule is written — a preference that a rule could override would not be a
    /// preference.
    /// </para>
    /// </summary>
    private static bool WantsDigest(
        NotificationRule rule, NotificationCategory? category, NotificationPreference? preference)
    {
        if (preference is null)
        {
            return rule.Policy == DeliveryPolicy.Digest;
        }

        if (!preference.Digest.IsActive)
        {
            return false;
        }

        if (rule.Policy == DeliveryPolicy.Digest)
        {
            return true;
        }

        var categoryPreference = preference.CategoryFor(rule.CategoryKey);

        return categoryPreference.UseDigest || category?.DigestByDefault == true;
    }

    /// <summary>
    /// Pushes a delivery moment past the end of the member's quiet hours.
    /// <para>
    /// <b>Computed in the member's own wall clock and converted back, which is the only way this can
    /// be right.</b> "22:00 to 07:00" is a statement about when somebody sleeps; a member in Lagos
    /// and one in Toronto who both say it mean instants five hours apart, and either would be woken
    /// by a policy evaluated in UTC. It also has to survive the clocks changing, which is why the
    /// conversion happens at evaluation time rather than being stored.
    /// </para>
    /// <para>
    /// An unknown time zone leaves the moment alone rather than failing. A stale zone identifier
    /// must never cost somebody their message.
    /// </para>
    /// </summary>
    private static DateTimeOffset ApplyQuietHours(
        NotificationPreference preference, DateTimeOffset moment)
    {
        if (!preference.QuietHours.IsEnabled)
        {
            return moment;
        }

        if (!TimeZoneInfo.TryFindSystemTimeZoneById(preference.TimeZoneId, out var zone))
        {
            return moment;
        }

        var local = TimeZoneInfo.ConvertTime(moment, zone);

        if (!preference.QuietHours.Contains(local.Hour))
        {
            return moment;
        }

        var localEnd = preference.QuietHours.NextEndAfter(local);

        return TimeZoneInfo.ConvertTime(localEnd, TimeZoneInfo.Utc);
    }
}
