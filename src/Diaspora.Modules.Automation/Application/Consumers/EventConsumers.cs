using System.Globalization;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.CommunityProgramme.V1.Events;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Domain.Catalog;
using Diaspora.Modules.Automation.Domain.Enums;
using Diaspora.Modules.Automation.Domain.Repositories;
using Diaspora.Modules.Automation.Domain.Scheduling;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.Automation.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-007 · GAP-013 · GAP-014 — events, communities and organizations.
//
// Five of the six event triggers have a published contract behind them; the sixth, `startingSoon`, is
// a projection over start times rather than anything anybody publishes, and lives in the reminder
// store. B12.1's report records the same split.
//
// **`event.created` is not here any more — 21 Oct 2026.** It consumed `EventCreated`, the draft, and
// fired it for every member's automation: a private draft's title reached strangers' notifications,
// and announced events that were never published. It fires on first publication now, from the
// publication consumer in `PublicationConsumers.cs` (one consumer per contract), and every event
// trigger that speaks to anybody watching asks `EventAudience` who that may be.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// An event's content changed.
/// <para>
/// Also moves every pending reminder on it. An event rescheduled from Friday to Saturday must cancel
/// the Friday reminder rather than fire it, and re-derive Saturday's — doing both in one consumer
/// keeps the two halves from diverging.
/// </para>
/// </summary>
public sealed class EventUpdatedAutomationConsumer
    : AutomationEventConsumer<EventUpdatedIntegrationEvent>
{
    private readonly IAutomationReminderRepository _reminders;
    private readonly AutomationSubjectHorizonService _horizons;
    private readonly IClock _clock;

    public EventUpdatedAutomationConsumer(
        AutomationEngine engine,
        IAutomationReminderRepository reminders,
        AutomationSubjectHorizonService horizons,
        IClock clock)
        : base(engine)
    {
        _reminders = reminders;
        _horizons = horizons;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        EventUpdatedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await base.HandleAsync(integrationEvent, cancellationToken);

        var visibilityChanged = integrationEvent.ChangedFields.Contains(
            "Visibility", StringComparer.OrdinalIgnoreCase);

        if (!integrationEvent.IsMaterialChange && !visibilityChanged)
        {
            // A corrected typo does not move a reminder. Rebasing on every edit would rewrite a
            // member's reminder for a change that did not affect when they need to leave.
            //
            // A visibility change does not move one either, but it changes who the horizon may
            // speak to — a public event made unlisted must stop announcing itself to strangers —
            // so it re-arms the horizon below. Reminders are the member's own and stay put.
            return;
        }

        var audience = EventAudience.For(
            integrationEvent.IsListedPublicly,
            integrationEvent.OrganizerUserId,
            integrationEvent.OrganizationId);

        // **Before the reminder rebase, and before its early return.** An event with no reminders
        // on it still has an `event.startingSoon` horizon: a member who set that trigger up in the
        // builder rather than from the event screen has no reminder row at all. Rebasing only the
        // reminders would leave the horizon on the old date, to fire while the event is elsewhere.
        //
        // `ArmAsync` replaces rather than duplicates — a horizon already written for an earlier
        // date is cancelled rather than left pending.
        if (integrationEvent.StartsAt > _clock.UtcNow && audience is not null)
        {
            var horizonValues = Values();

            await _horizons.ArmAsync(
                "event.startingSoon",
                AutomationSubjectKinds.Event,
                integrationEvent.CommunityEventId,
                integrationEvent.Title,
                integrationEvent.StartsAt,
                null,
                audience,
                horizonValues,
                $"{integrationEvent.Title} is starting soon.",
                cancellationToken);
        }
        else
        {
            // Moved into the past. The same judgement the reminder rebase makes below: cancelled
            // rather than fired, because a "starting soon" arriving afterwards is worse than
            // silence. Or there is nobody left it may speak to.
            await _horizons.CancelForSubjectAsync(
                integrationEvent.CommunityEventId,
                audience is null
                    ? "Nobody watching may see the event any more."
                    : "The event was moved to a time that has already passed.",
                cancellationToken);
        }

        if (!integrationEvent.IsMaterialChange)
        {
            return;
        }

        var pending = await _reminders.ListPendingBySubjectAsync(
            integrationEvent.CommunityEventId, cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;

        foreach (var reminder in pending)
        {
            var dueAt = integrationEvent.StartsAt.AddMinutes(-reminder.OffsetMinutes);

            if (dueAt <= now)
            {
                // The event moved close enough that the reminder's moment has passed. Cancelled
                // rather than fired: a "starts in an hour" that arrives after it started is worse
                // than silence, and the member can see it was cancelled.
                reminder.Cancel();
                continue;
            }

            reminder.Reschedule(integrationEvent.StartsAt, dueAt);
        }

        await _reminders.SaveChangesAsync(cancellationToken);
    }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        EventUpdatedIntegrationEvent integrationEvent)
    {
        // Somebody else's unlisted, members-only or private event is not news to anybody watching.
        if (EventAudience.For(
                integrationEvent.IsListedPublicly,
                integrationEvent.OrganizerUserId,
                integrationEvent.OrganizationId) is not { } audience)
        {
            yield break;
        }

        var values = Values();
        values["eventType"] = "updated";
        values["dayOfWeek"] = ((int)integrationEvent.StartsAt.DayOfWeek).ToString(CultureInfo.InvariantCulture);
        values["timeOfDay"] = integrationEvent.StartsAt.ToString("HH:mm", CultureInfo.InvariantCulture);

        yield return Context(
            "event.updated",
            integrationEvent.EventId,
            audience,
            integrationEvent.IsMaterialChange
                ? $"{integrationEvent.Title} moved."
                : $"{integrationEvent.Title} changed.",
            integrationEvent.OccurredAt,
            "event",
            integrationEvent.CommunityEventId,
            integrationEvent.Title,
            integrationEvent.StartsAt,
            integrationEvent.TimeZoneId,
            values);
    }
}

/// <summary>An event was cancelled. Cancels every pending reminder on it.</summary>
public sealed class EventCancelledAutomationConsumer
    : AutomationEventConsumer<EventCancelledIntegrationEvent>
{
    private readonly IAutomationReminderRepository _reminders;
    private readonly AutomationSubjectHorizonService _horizons;

    public EventCancelledAutomationConsumer(
        AutomationEngine engine,
        IAutomationReminderRepository reminders,
        AutomationSubjectHorizonService horizons)
        : base(engine)
    {
        _reminders = reminders;
        _horizons = horizons;
    }

    protected override async Task HandleAsync(
        EventCancelledIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await base.HandleAsync(integrationEvent, cancellationToken);

        // The reminders go, and so does the horizon behind `event.startingSoon`. By subject rather
        // than by trigger key, for the reason the service documents: a caller naming each one would
        // eventually forget one and remind somebody about an event that is not happening.
        await _horizons.CancelForSubjectAsync(
            integrationEvent.CommunityEventId,
            "The event was cancelled.",
            cancellationToken);

        var pending = await _reminders.ListPendingBySubjectAsync(
            integrationEvent.CommunityEventId, cancellationToken);

        foreach (var reminder in pending)
        {
            reminder.Cancel();
        }

        if (pending.Count > 0)
        {
            await _reminders.SaveChangesAsync(cancellationToken);
        }
    }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        EventCancelledIntegrationEvent integrationEvent)
    {
        // A cancelled draft — or an event only its own audience could see — was never anybody
        // else's to hear about. The title is the leak: "X was cancelled" names it.
        if (EventAudience.For(
                integrationEvent.IsListedPublicly,
                integrationEvent.OrganizerUserId,
                integrationEvent.OrganizationId) is not { } audience)
        {
            yield break;
        }

        var values = Values();
        values["eventType"] = "cancelled";

        yield return Context(
            "event.cancelled",
            integrationEvent.EventId,
            audience,
            $"{integrationEvent.Title} was cancelled.",
            integrationEvent.OccurredAt,
            "event",
            integrationEvent.CommunityEventId,
            integrationEvent.Title,
            integrationEvent.StartsAt,
            null,
            values);
    }
}

/// <summary>
/// A member registered for an event.
/// <para>
/// <b>One contract, two triggers, and they concern different people</b> — the attendee's own
/// <c>event.rsvpConfirmed</c>, and the organizing body's <c>organization.registrationReceived</c>.
/// Wave 12 adds the second: Wave 11 recorded it as waiting on a CommunityLife contract named
/// <c>OrganizationRegistrationReceived</c>, and the fact was already on the wire —
/// <c>RsvpSubmittedIntegrationEvent</c> has carried <c>OrganizationId</c> since B12. "Someone
/// registers for our event" is this event, read from the organization's side.
/// </para>
/// <para>
/// Two contexts from one consumer rather than a second consumer over the same contract, for the
/// reason the job-application consumer states: two settlement owners inside one module for one event
/// is what DSP1003 reports, and an at-least-once redelivery would apply twice.
/// </para>
/// </summary>
public sealed class RsvpSubmittedAutomationConsumer
    : AutomationEventConsumer<RsvpSubmittedIntegrationEvent>
{
    public RsvpSubmittedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        RsvpSubmittedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["eventType"] = integrationEvent.Status;

        yield return Context(
            "event.rsvpConfirmed",
            integrationEvent.EventId,

            // This one concerns the attendee specifically: it is their registration and their rule.
            Owners(integrationEvent.AttendeeUserId),
            integrationEvent.EventTitle is { Length: > 0 } title
                ? $"You registered for {title}."
                : "You registered for an event.",
            integrationEvent.OccurredAt,
            "event",
            integrationEvent.CommunityEventId,
            integrationEvent.EventTitle,
            integrationEvent.EventStartsAt,
            null,
            values);

        if (integrationEvent.OrganizationId is not { } organizationId ||
            organizationId == Guid.Empty)
        {
            // An event nobody's organization is running. The attendee still gets their own trigger;
            // there is simply no body for the second one to concern.
            yield break;
        }

        var organizationValues = Values();
        organizationValues["eventType"] = integrationEvent.Status;
        organizationValues["countAbove"] = integrationEvent.GoingCount
            .ToString(CultureInfo.InvariantCulture);

        yield return Context(
            "organization.registrationReceived",
            integrationEvent.EventId,

            // The organization, and the administrator B13.1 named on the contract, so an automation
            // owned by either resolves. Both are on the payload already.
            Owners(organizationId, integrationEvent.OrganizerUserId),

            // No name, and that is deliberate — §50's rule, which Wave 11's organization-joined
            // consumer states in the same words: a broadcast about somebody registering must not
            // carry who registered. The count is the organization's own figure.
            integrationEvent.EventTitle is { Length: > 0 } eventTitle
                ? $"Somebody registered for {eventTitle}."
                : "Somebody registered for one of your events.",
            integrationEvent.OccurredAt,
            "event",
            integrationEvent.CommunityEventId,
            integrationEvent.EventTitle,
            integrationEvent.EventStartsAt,
            null,
            organizationValues);
    }
}

/// <summary>A date in a repeating event moved. Rebases every pending reminder on that date.</summary>
public sealed class EventOccurrenceRescheduledAutomationConsumer
    : AutomationEventConsumer<EventOccurrenceRescheduledIntegrationEvent>
{
    private readonly IAutomationReminderRepository _reminders;
    private readonly IClock _clock;

    public EventOccurrenceRescheduledAutomationConsumer(
        AutomationEngine engine, IAutomationReminderRepository reminders, IClock clock)
        : base(engine)
    {
        _reminders = reminders;
        _clock = clock;
    }

    protected override async Task HandleAsync(
        EventOccurrenceRescheduledIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        await base.HandleAsync(integrationEvent, cancellationToken);

        var pending = await _reminders.ListPendingBySubjectAsync(
            integrationEvent.CommunityEventId, cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;

        foreach (var reminder in pending)
        {
            var dueAt = integrationEvent.CurrentStartsAt.AddMinutes(-reminder.OffsetMinutes);

            if (dueAt <= now)
            {
                reminder.Cancel();
                continue;
            }

            reminder.Reschedule(integrationEvent.CurrentStartsAt, dueAt);
        }

        await _reminders.SaveChangesAsync(cancellationToken);
    }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        EventOccurrenceRescheduledIntegrationEvent integrationEvent)
    {
        // A series' dates can be drafts. Same rule as every other event trigger.
        if (EventAudience.For(
                integrationEvent.IsListedPublicly,
                integrationEvent.OrganizerUserId,
                integrationEvent.OrganizationId) is not { } audience)
        {
            yield break;
        }

        var values = Values();
        values["eventType"] = "rescheduled";
        values["dayOfWeek"] = ((int)integrationEvent.CurrentStartsAt.DayOfWeek).ToString(CultureInfo.InvariantCulture);

        yield return Context(
            "event.occurrenceChanged",
            integrationEvent.EventId,
            audience,
            $"A date in {integrationEvent.Title} moved.",
            integrationEvent.OccurredAt,
            "event",
            integrationEvent.CommunityEventId,
            integrationEvent.Title,
            integrationEvent.CurrentStartsAt,
            integrationEvent.TimeZoneId,
            values);
    }
}

/// <summary>Somebody joined an organization. Concerns the organization.</summary>
public sealed class OrganizationMemberJoinedAutomationConsumer
    : AutomationEventConsumer<OrganizationMemberJoinedIntegrationEvent>
{
    public OrganizationMemberJoinedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        OrganizationMemberJoinedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["countAbove"] = integrationEvent.MemberCount.ToString(CultureInfo.InvariantCulture);

        yield return Context(
            "organization.memberJoined",
            integrationEvent.EventId,

            // The organization is the owner an automation would be written against.
            Owners(integrationEvent.OrganizationId),

            // No name, and that is deliberate. §50: a broadcast about somebody joining must not carry
            // who joined, because a member's arrival is theirs to announce.
            "Somebody new joined.",
            integrationEvent.OccurredAt,
            "organization",
            integrationEvent.OrganizationId,
            null,
            null,
            null,
            values);
    }
}

/// <summary>A membership's status changed — including one that is due to lapse.</summary>
public sealed class ProgrammeMembershipAutomationConsumer
    : AutomationEventConsumer<ProgrammeMembershipStatusChangedIntegrationEvent>
{
    public ProgrammeMembershipAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        ProgrammeMembershipStatusChangedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["applicationStatus"] = integrationEvent.CurrentStatus;

        yield return Context(
            "organization.membershipRenewalDue",
            integrationEvent.EventId,
            Owners(integrationEvent.OrganizationId),
            "A membership needs attention.",
            integrationEvent.OccurredAt,
            "membership",
            integrationEvent.MembershipId,
            null,
            integrationEvent.ExpiresAt,
            null,
            values);
    }
}

/// <summary>A community announcement was published. Concerns the scope it names.</summary>
public sealed class CommunityAnnouncementAutomationConsumer
    : AutomationEventConsumer<CommunityAnnouncementPublishedIntegrationEvent>
{
    public CommunityAnnouncementAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        CommunityAnnouncementPublishedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["urgency"] = "normal";
        values["community"] = integrationEvent.ScopeId?.ToString("D");

        yield return Context(
            "community.announcementPublished",
            integrationEvent.EventId,
            Owners(integrationEvent.ScopeId, integrationEvent.OrganizationId),
            integrationEvent.Title,
            integrationEvent.OccurredAt,
            "announcement",
            integrationEvent.AnnouncementId,
            integrationEvent.Title,
            null,
            null,
            values);
    }
}

/// <summary>
/// A community reached a size it had never reached — 11 Sep 2026.
/// <para>
/// <b>The last of the four events the Backend Gap Build Order called genuinely missing.</b> It
/// waited on two things. The contract is published once per milestone, ever, from
/// <c>Community.RecordMemberJoined</c>; and the automation has somebody to belong to, since founding
/// a community makes its founder the Owner and an Administrator.
/// </para>
/// <para>
/// The community is the concerned owner, so a community-owned automation — owned by its Owner or an
/// Administrator — matches. The member count rides in <c>countAbove</c>, so a rule can say "only from
/// a thousand".
/// </para>
/// </summary>
public sealed class CommunityMilestoneAutomationConsumer
    : AutomationEventConsumer<CommunityMilestoneReachedIntegrationEvent>
{
    public CommunityMilestoneAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        CommunityMilestoneReachedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["countAbove"] = integrationEvent.MemberCount.ToString(CultureInfo.InvariantCulture);
        values["community"] = integrationEvent.CommunityId.ToString("D");

        var milestone = integrationEvent.Milestone.ToString("N0", CultureInfo.InvariantCulture);

        yield return Context(
            "community.milestoneReached",
            integrationEvent.EventId,
            Owners(integrationEvent.CommunityId),
            $"{integrationEvent.CommunityName} reached {milestone} members.",
            integrationEvent.OccurredAt,
            "community",
            integrationEvent.CommunityId,
            integrationEvent.CommunityName,
            null,
            null,
            values);
    }
}

/// <summary>
/// Somebody joined a community — 21 Oct 2026, <c>community.memberJoined</c>.
/// <para>
/// The community is the concerned owner, so a community-owned automation — owned by its Owner or an
/// Administrator — matches. One join is one run: the contract is published once per transition into
/// membership, and its <c>EventId</c> is the idempotency key like every other.
/// </para>
/// <para>
/// <b>Nobody's identity reaches the automation, whatever the member's visibility.</b> A Private
/// member's belonging is hidden even from the community's administrators on its members list, so a
/// run that named them — in its summary, its subject or a condition value — would tell the
/// automation's owner, and every recipient of <c>notify.community</c>, what the platform refuses to
/// tell them anywhere else. The visible members could be named, but a rule that names some joiners
/// and not others tells the owner which ones are private, so none are: the summary says somebody
/// joined, as <c>organization.memberJoined</c> has always done. The subject is the community.
/// </para>
/// </summary>
public sealed class CommunityMemberJoinedAutomationConsumer
    : AutomationEventConsumer<CommunityMemberJoinedIntegrationEvent>
{
    public CommunityMemberJoinedAutomationConsumer(AutomationEngine engine) : base(engine) { }

    protected override IEnumerable<AutomationTriggerContext> Translate(
        CommunityMemberJoinedIntegrationEvent integrationEvent)
    {
        var values = Values();
        values["community"] = integrationEvent.CommunityId.ToString("D");

        yield return Context(
            "community.memberJoined",
            integrationEvent.EventId,
            Owners(integrationEvent.CommunityId),
            "Somebody new joined the community.",
            integrationEvent.OccurredAt,
            "community",
            integrationEvent.CommunityId,
            null,
            null,
            null,
            values);
    }
}
