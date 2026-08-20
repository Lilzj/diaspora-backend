using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.Modules.Notifications.Extensibility.Realtime;
using Diaspora.Modules.Notifications.Infrastructure.Realtime;
using Diaspora.Modules.Notifications.Realtime;
using Diaspora.Platform.Communication;
using Diaspora.SharedKernel.Identity;
using Microsoft.Extensions.Logging;

namespace Diaspora.Modules.Notifications.Application.Consumers.CommunityLife;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The three facts about an event that change somebody's plans, and what B13.3 does with each.
//
// All three were published by Community Life and consumed by nothing. An event could be cancelled
// an hour before the doors and the only way to find out was to open the app — which is the exact
// class of gap the frontend's Phase 12 register recorded, and the reason its screens say "updates
// every 30 seconds, not instantly".
//
// Each does two things and they are deliberately different in kind:
//
//   1. A real-time frame to event:{id}, which reaches whoever has the screen open. Best-effort,
//      swallowed, and authorised by Community Life through the kernel's channel seam — this module
//      never learns what an RSVP is.
//
//   2. A notification per attendee, resolved through the same seam's bounded subscriber list. That
//      is what reaches the member whose phone is in a pocket, and it is the one that matters.
//
// The audience is resolved rather than carried on the contract. B12 publishes AffectedAttendeeCount
// and not a guest list, correctly: a contract that carried every attendee would grow without bound
// and would put a list of who is going to a private event onto a bus every module can read.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The shared half of the three consumers below: resolve who is affected, tell them, and announce it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A cap with a log line rather than a cap in silence.</b> The fan-out happens inside one consumed
/// event's unit of work, so it is bounded — and a bound nobody is told about is indistinguishable
/// from a delivery that worked. An event whose attendance reaches the cap logs it with the event's
/// identifier, which is what an operator needs to answer "why did four hundred people hear and six
/// hundred not".
/// </para>
/// </remarks>
internal static class EventAudience
{
    /// <summary>
    /// The most attendees one consumed event will notify.
    /// <para>
    /// Matches the Knowledge consumers' audience limit, and for the same reason: the dispatcher
    /// writes a row per recipient and the whole fan-out is saved in one unit of work at the end of
    /// the handler. Community Life caps its own answer lower still.
    /// </para>
    /// </summary>
    public const int Limit = 500;

    public static async Task<IReadOnlyList<UserId>> ResolveAsync(
        RealtimeChannelRegistry channels,
        Guid communityEventId,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var attendees = await channels.ListSubscribersAsync(
            RealtimeChannels.ForCommunityEvent(communityEventId), Limit, cancellationToken);

        if (attendees.Count >= Limit)
        {
            logger.LogWarning(
                "Event {CommunityEventId} has at least {Limit} live responses and only that many "
                + "were notified. The rest were not told and nothing else will tell them.",
                communityEventId,
                Limit);
        }

        return attendees;
    }
}

/// <summary>
/// The event is off.
/// <para>
/// <b>High rather than Critical, and the distinction is about sleep.</b> Critical overrides quiet
/// hours; a cancellation somebody will read at breakfast does not earn that, and a platform that
/// woke members at three in the morning to say a Saturday picnic was off would have taught them to
/// switch the category away entirely.
/// </para>
/// <para>
/// <b>A moderation cancellation is still told to the attendees.</b> They rearranged their week around
/// it and the event is equally not happening; what they are not told is that a moderator did it,
/// which is a fact about the organizer rather than about the event.
/// </para>
/// </summary>
public sealed class EventCancelledConsumer : ModuleEventConsumer<EventCancelledIntegrationEvent>
{
    private readonly RealtimeChannelRegistry _channels;
    private readonly RealtimeChannelBroadcaster _broadcaster;
    private readonly ILogger<EventCancelledConsumer> _logger;

    public EventCancelledConsumer(
        INotificationDispatcher dispatcher,
        RealtimeChannelRegistry channels,
        RealtimeChannelBroadcaster broadcaster,
        ILogger<EventCancelledConsumer> logger)
        : base(dispatcher)
    {
        _channels = channels;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    protected override string SourceModule => "CommunityLife";

    protected override IEnumerable<NotificationTrigger> Translate(
        EventCancelledIntegrationEvent integrationEvent) => Translate(integrationEvent, []);

    protected override async Task HandleAsync(
        EventCancelledIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Live first, notifications second. Somebody looking at the event should see it change
        // before their phone considers buzzing, and the notification path is the slower of the two —
        // it resolves an audience, then consults preferences, quiet hours and a throttle for each.
        await _broadcaster.ResourceChangedAsync(
            RealtimeChannels.ForCommunityEvent(integrationEvent.CommunityEventId),
            new ResourceChanged(
                "event",
                integrationEvent.CommunityEventId.ToString(),
                ResourceChanged.Kinds.Cancelled,

                // A stable key, never the organizer's own words. The reason a human typed is
                // content: it is subject to moderation and to erasure, and a socket frame is the one
                // place neither can reach it.
                integrationEvent.IsModerationAction ? "moderation" : "organizer",
                integrationEvent.CancelledAt),
            cancellationToken);

        var attendees = await EventAudience.ResolveAsync(
            _channels, integrationEvent.CommunityEventId, _logger, cancellationToken);

        foreach (var trigger in Translate(integrationEvent, attendees))
        {
            await DispatchOneAsync(trigger, cancellationToken);
        }
    }

    private IEnumerable<NotificationTrigger> Translate(
        EventCancelledIntegrationEvent integrationEvent, IReadOnlyList<UserId> attendees)
    {
        var values = Values();
        values["title"] = integrationEvent.Title;
        values["eventId"] = integrationEvent.CommunityEventId;
        values["startsAt"] = integrationEvent.StartsAt;

        foreach (var attendee in attendees)
        {
            yield return Trigger(
                attendee,
                "attendee",
                integrationEvent.CancelledAt,
                values,
                subjectType: "event",
                subjectId: integrationEvent.CommunityEventId.ToString());
        }
    }
}

/// <summary>
/// One occurrence of a repeating event moved.
/// <para>
/// <b>The occurrence's own channel, not the series'.</b> B12.1 settled that an occurrence is an
/// event, so the members who need telling are the ones who answered for <em>this</em> date —
/// broadcasting to the series would tell somebody about a Tuesday they had not signed up for.
/// </para>
/// </summary>
public sealed class EventOccurrenceRescheduledConsumer
    : ModuleEventConsumer<EventOccurrenceRescheduledIntegrationEvent>
{
    private readonly RealtimeChannelRegistry _channels;
    private readonly RealtimeChannelBroadcaster _broadcaster;
    private readonly ILogger<EventOccurrenceRescheduledConsumer> _logger;

    public EventOccurrenceRescheduledConsumer(
        INotificationDispatcher dispatcher,
        RealtimeChannelRegistry channels,
        RealtimeChannelBroadcaster broadcaster,
        ILogger<EventOccurrenceRescheduledConsumer> logger)
        : base(dispatcher)
    {
        _channels = channels;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    protected override string SourceModule => "CommunityLife";

    protected override IEnumerable<NotificationTrigger> Translate(
        EventOccurrenceRescheduledIntegrationEvent integrationEvent) => Translate(integrationEvent, []);

    protected override async Task HandleAsync(
        EventOccurrenceRescheduledIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _broadcaster.ResourceChangedAsync(
            RealtimeChannels.ForCommunityEvent(integrationEvent.CommunityEventId),
            new ResourceChanged(
                "event",
                integrationEvent.CommunityEventId.ToString(),
                ResourceChanged.Kinds.Rescheduled,
                Reason: null,
                integrationEvent.RescheduledAt),
            cancellationToken);

        var attendees = await EventAudience.ResolveAsync(
            _channels, integrationEvent.CommunityEventId, _logger, cancellationToken);

        foreach (var trigger in Translate(integrationEvent, attendees))
        {
            await DispatchOneAsync(trigger, cancellationToken);
        }
    }

    private IEnumerable<NotificationTrigger> Translate(
        EventOccurrenceRescheduledIntegrationEvent integrationEvent,
        IReadOnlyList<UserId> attendees)
    {
        var values = Values();
        values["title"] = integrationEvent.Title;
        values["eventId"] = integrationEvent.CommunityEventId;

        // The new start, rendered in the reader's own zone by the renderer. The contract also
        // carries the previous one and the event's own zone; neither belongs in the message, because
        // "it moved from 6pm to 7pm" is two times for a reader to reconcile against a third.
        values["startsAt"] = integrationEvent.CurrentStartsAt;

        foreach (var attendee in attendees)
        {
            yield return Trigger(
                attendee,
                "attendee",
                integrationEvent.RescheduledAt,
                values,
                subjectType: "event",
                subjectId: integrationEvent.CommunityEventId.ToString());
        }
    }
}

/// <summary>
/// Something about the event changed.
/// <para>
/// <b>Only a material change produces anything, and the contract says which those are.</b> B12 sets
/// <c>IsMaterialChange</c> when the change is one an attendee would want to know about — the venue,
/// the time, the name. A member told about a corrected typo stops reading the ones about a new venue,
/// and the judgement of which is which belongs to the module that owns the record rather than to a
/// list of field names maintained here.
/// </para>
/// <para>
/// <b>The live frame is sent for every change, material or not.</b> It costs one frame, carries no
/// interruption, and a client with the event open should show what it now says — the whole reason the
/// two paths are separate is that one is free and the other is somebody's attention.
/// </para>
/// </summary>
public sealed class EventUpdatedConsumer : ModuleEventConsumer<EventUpdatedIntegrationEvent>
{
    private readonly RealtimeChannelRegistry _channels;
    private readonly RealtimeChannelBroadcaster _broadcaster;
    private readonly ILogger<EventUpdatedConsumer> _logger;

    public EventUpdatedConsumer(
        INotificationDispatcher dispatcher,
        RealtimeChannelRegistry channels,
        RealtimeChannelBroadcaster broadcaster,
        ILogger<EventUpdatedConsumer> logger)
        : base(dispatcher)
    {
        _channels = channels;
        _broadcaster = broadcaster;
        _logger = logger;
    }

    protected override string SourceModule => "CommunityLife";

    protected override IEnumerable<NotificationTrigger> Translate(
        EventUpdatedIntegrationEvent integrationEvent) => Translate(integrationEvent, []);

    protected override async Task HandleAsync(
        EventUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await _broadcaster.ResourceChangedAsync(
            RealtimeChannels.ForCommunityEvent(integrationEvent.CommunityEventId),
            new ResourceChanged(
                "event",
                integrationEvent.CommunityEventId.ToString(),
                ResourceChanged.Kinds.Updated,
                Reason: null,
                integrationEvent.UpdatedAt),
            cancellationToken);

        if (!integrationEvent.IsMaterialChange)
        {
            return;
        }

        var attendees = await EventAudience.ResolveAsync(
            _channels, integrationEvent.CommunityEventId, _logger, cancellationToken);

        foreach (var trigger in Translate(integrationEvent, attendees))
        {
            await DispatchOneAsync(trigger, cancellationToken);
        }
    }

    private IEnumerable<NotificationTrigger> Translate(
        EventUpdatedIntegrationEvent integrationEvent, IReadOnlyList<UserId> attendees)
    {
        var values = Values();
        values["title"] = integrationEvent.Title;
        values["eventId"] = integrationEvent.CommunityEventId;

        foreach (var attendee in attendees)
        {
            yield return Trigger(
                attendee,
                "attendee",
                integrationEvent.UpdatedAt,
                values,
                subjectType: "event",
                subjectId: integrationEvent.CommunityEventId.ToString());
        }
    }
}
