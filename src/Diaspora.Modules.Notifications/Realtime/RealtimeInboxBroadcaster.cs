using Diaspora.Modules.Notifications.Domain.Inboxes;
using Diaspora.Modules.Notifications.Extensibility.Channels;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Observability;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Realtime;

/// <summary>
/// Pushes an inbox arrival to whichever of the member's clients are connected.
/// <para>
/// <b>The live view of a row the member can already read, and deliberately not a second channel.</b>
/// Nothing here decides whether a message should be sent — by the time this runs the rule has
/// matched, preferences have been consulted, quiet hours have been applied and the notification is
/// committed. It sends what the in-app channel was going to make readable, a moment sooner than a
/// poll would.
/// </para>
/// <para>
/// <b>Why real-time is not a seventh <c>NotificationChannelKind</c>.</b> It looks like one, and the
/// preference model says otherwise: <c>CategoryPreference.Allows</c> returns false for any channel
/// the member has never been asked about, so a new routed kind would be suppressed for every
/// non-critical notification until the preference aggregate, its table, its wire contract and every
/// seeded rule changed together. And it would be the wrong shape anyway — quiet hours would
/// <em>defer</em> a live push to a socket that will not exist by then, and a member would face two
/// switches, "in-app" and "real-time", for one message. Real-time is not a destination somebody
/// chooses between; it is how the in-app channel reaches a client that is already listening. The
/// transport behind it stays replaceable through <see cref="IRealtimeNotifier"/>, which is where
/// provider independence actually lives.
/// </para>
/// <para>
/// <b>The counter read is a read of the member's own committed badge, not a policy decision.</b> The
/// channel contract forbids a channel consulting preferences because that would be a second place
/// the send/do-not-send answer could differ. A count is not an answer to that question — and sending
/// the notification without it would leave every client incrementing its own badge and disagreeing
/// with the inbox screen the first time one was missed.
/// </para>
/// </summary>
public sealed class RealtimeInboxBroadcaster
{
    private readonly IRealtimeNotifier _notifier;
    private readonly IRealtimeConnectionRegistry _connections;
    private readonly IUserInboxRepository _inboxes;
    private readonly IClock _clock;

    public RealtimeInboxBroadcaster(
        IRealtimeNotifier notifier,
        IRealtimeConnectionRegistry connections,
        IUserInboxRepository inboxes,
        IClock clock)
    {
        _notifier = notifier;
        _connections = connections;
        _inboxes = inboxes;
        _clock = clock;
    }

    /// <summary>
    /// Sends the arrival and the new badge to every socket the member holds.
    /// <para>
    /// Returns without doing anything when real-time is switched off or the member has no live
    /// client — which is the common case and costs one dictionary lookup, not a database read. The
    /// order matters: the notification first, the counters second, so a client that renders on the
    /// first event never briefly shows a badge that has not caught up.
    /// </para>
    /// </summary>
    public async Task BroadcastArrivalAsync(
        NotificationChannelRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!_notifier.IsEnabled
            || !await _connections.IsConnectedAsync(request.RecipientUserId, cancellationToken))
        {
            return;
        }

        await _notifier.SendToUserAsync(
            request.RecipientUserId,
            RealtimeEvents.NotificationReceived,
            new RealtimeNotification(
                request.NotificationId.Value,
                request.Title,
                request.Body,
                request.CategoryKey,
                request.Priority.ToString(),
                _clock.UtcNow,
                request.Route,
                request.ImageMediaId),
            cancellationToken);

        NotificationTelemetry.RealtimeDelivered.Add(
            1, new KeyValuePair<string, object?>("category", request.CategoryKey));

        await BroadcastCountersAsync(request.RecipientUserId, cancellationToken);
    }

    /// <summary>
    /// Sends the member's current badge.
    /// <para>
    /// Reads the counter aggregate rather than the notification table: the maintained counters are
    /// what the inbox screen renders, so pushing a freshly computed number would make the badge and
    /// the screen disagree whenever the counters had drifted — and hide the drift the reconciliation
    /// sweep exists to repair.
    /// </para>
    /// </summary>
    public async Task BroadcastCountersAsync(
        UserId userId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userId);

        if (!_notifier.IsEnabled)
        {
            return;
        }

        var inbox = await _inboxes.FindAsync(userId, cancellationToken);

        if (inbox is null)
        {
            return;
        }

        var byCategory = inbox.Categories
            .Where(counter => counter.UnreadCount > 0)
            .ToDictionary(
                counter => counter.CategoryKey,
                counter => counter.UnreadCount,
                StringComparer.Ordinal);

        await _notifier.SendToUserAsync(
            userId,
            RealtimeEvents.InboxCountersChanged,
            new InboxCounters(inbox.UnreadCount, byCategory, _clock.UtcNow),
            cancellationToken);
    }
}
