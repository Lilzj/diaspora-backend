using System.Globalization;
using Diaspora.Application.Abstractions;
using Diaspora.Contracts.CommunityLife.V1.Events;
using Diaspora.Contracts.Events;
using Diaspora.Contracts.Housing.V1.Events;
using Diaspora.Contracts.Jobs.V1.Events;
using Diaspora.Contracts.Marketplace.V1.Events;
using Diaspora.Modules.Automation.Application.Engine;
using Diaspora.Modules.Automation.Application.Services;
using Diaspora.Modules.Automation.Domain.Catalog;

namespace Diaspora.Modules.Automation.Application.Consumers;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// FRONTEND-PHASE18-GAP-010 · GAP-011 · GAP-012 — the four scheduled projections over somebody
// else's date. W12-GAP-002 · 003 · 004 · 005.
//
// Wave 11 recorded all four as owning-module responsibilities: "Jobs publishes no closing horizon",
// "Marketplace publishes no expiry horizon", "Marketplace publishes no reservation end horizon",
// "Housing publishes no viewing appointment". Every one of those was assembled from the frontend
// register's "Backend event required" column rather than from the contracts assembly, and every one
// is wrong against the code:
//
//   JobPublishedIntegrationEvent.ClosingAt        — "When applications stop."
//   ListingPublishedIntegrationEvent.ExpiresAt
//   ListingReservedIntegrationEvent.ExpiresAt
//   ViewingScheduledIntegrationEvent.ScheduledFor — published by HousingIntegrationEventRelay
//
// So none of the four needs an owning-module change. Each consumer reads a date that is already on
// the wire and asks the horizon store to come back before it.
//
// **The cancellation half is as important as the arming half.** A job closed early, a listing sold
// or was archived, a reservation was cancelled, a viewing was completed or called off — each of
// those is also a published fact, and a horizon that is not cancelled reminds a member about
// something that is no longer going to happen. Every arming consumer has a cancelling twin.
//
// **Three of them are not in this file, and the guardrail is why.** `JobPublished`,
// `ListingPublished` and `ListingSold` already had a consumer in this module — the saved-search
// matchers — and `No_module_consumes_one_integration_event_twice` reports a second. It is right to:
// two settlement owners inside one module for one at-least-once fact is two things happening twice
// on a redelivery. Arming a horizon from those three contracts therefore happens inside the consumer
// that already owns them, in `PublicationConsumers.cs`, and the entries here are the contracts this
// module had no consumer for.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The shape the horizon consumers share.
/// <para>
/// Not derived from <see cref="AutomationEventConsumer{T}"/>: these produce no trigger context at
/// consumption time. They write a row, and the context is built at due time by the sweeper — which is
/// the whole reason one publication does not become one row per watcher.
/// </para>
/// </summary>
public abstract class SubjectHorizonConsumer<TIntegrationEvent>
    : IntegrationEventHandler<TIntegrationEvent>
    where TIntegrationEvent : IIntegrationEvent
{
    protected SubjectHorizonConsumer(AutomationSubjectHorizonService horizons) => Horizons = horizons;

    protected AutomationSubjectHorizonService Horizons { get; }

    protected static Dictionary<string, string?> Values() => AutomationTriggerContext.NewValues();

    protected static IReadOnlyList<Guid> Owners(params Guid?[] candidates) =>
        [.. candidates
            .Where(id => id is { } value && value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()];
}

// ── Jobs ────────────────────────────────────────────────────────────────────────────────────────

/// <summary>A posting closed. Whatever the horizon said, there is nothing left to warn about.</summary>
public sealed class JobClosedHorizonConsumer
    : SubjectHorizonConsumer<JobClosedIntegrationEvent>
{
    public JobClosedHorizonConsumer(AutomationSubjectHorizonService horizons) : base(horizons) { }

    protected override Task HandleAsync(
        JobClosedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Horizons.CancelForSubjectAsync(
            integrationEvent.JobId, "The posting closed.", cancellationToken);
    }
}

// ── Events ──────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// An event left circulation — archived, deleted, hidden or suspended by a moderator, or taken back
/// to draft — 21 Oct 2026.
/// <para>
/// <b>The <c>event.startingSoon</c> horizon had no cancelling twin for any of these.</b> Cancellation
/// and a move into the past were handled; an event hidden by a moderator, or unpublished by its
/// organiser, kept a horizon armed for anybody watching and would have announced it on the day.
/// </para>
/// </summary>
public sealed class EventArchivedHorizonConsumer
    : SubjectHorizonConsumer<EventArchivedIntegrationEvent>
{
    public EventArchivedHorizonConsumer(AutomationSubjectHorizonService horizons) : base(horizons) { }

    protected override Task HandleAsync(
        EventArchivedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Horizons.CancelForSubjectAsync(
            integrationEvent.CommunityEventId, "The event is no longer shown.", cancellationToken);
    }
}

// ── Marketplace ─────────────────────────────────────────────────────────────────────────────────

/// <summary>A listing was archived or expired. Same cancellation.</summary>
public sealed class ListingArchivedHorizonConsumer
    : SubjectHorizonConsumer<ListingArchivedIntegrationEvent>
{
    public ListingArchivedHorizonConsumer(AutomationSubjectHorizonService horizons) : base(horizons) { }

    protected override Task HandleAsync(
        ListingArchivedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Horizons.CancelForSubjectAsync(
            integrationEvent.ListingId, "The listing is no longer shown.", cancellationToken);
    }
}

/// <summary>
/// A reservation was taken with a hold that ends.
/// <para>
/// The subject is the <b>reservation</b> rather than the listing, because that is what ends — and
/// because a listing may carry several reservations, and a horizon keyed on the listing would let the
/// second overwrite the first.
/// </para>
/// </summary>
public sealed class ReservationEndingHorizonConsumer
    : SubjectHorizonConsumer<ListingReservedIntegrationEvent>
{
    public ReservationEndingHorizonConsumer(AutomationSubjectHorizonService horizons)
        : base(horizons) { }

    protected override Task HandleAsync(
        ListingReservedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        if (integrationEvent.ExpiresAt is not { } expiresAt)
        {
            return Task.CompletedTask;
        }

        var values = Values();
        values["listingStatus"] = "reserved";

        return Horizons.ArmAsync(
            "listing.reservationEnding",
            AutomationSubjectKinds.Reservation,
            integrationEvent.ReservationId,
            "A reservation on one of your listings",
            expiresAt,
            null,

            // The seller, as the profile and the member. The buyer's identity is never carried into an
            // automation about the seller's own listing — Wave 11's offer consumer records the same
            // rule.
            Owners(integrationEvent.SellerId, integrationEvent.SellerUserId),
            values,
            "A reservation on one of your listings is about to run out.",
            cancellationToken);
    }
}

/// <summary>A reservation ended early. Cancels its horizon.</summary>
public sealed class ReservationCancelledHorizonConsumer
    : SubjectHorizonConsumer<global::Diaspora.Contracts.Marketplace.V1.Events.ReservationCancelledIntegrationEvent>
{
    public ReservationCancelledHorizonConsumer(AutomationSubjectHorizonService horizons)
        : base(horizons) { }

    protected override Task HandleAsync(
        global::Diaspora.Contracts.Marketplace.V1.Events.ReservationCancelledIntegrationEvent integrationEvent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Horizons.CancelForSubjectAsync(
            integrationEvent.ReservationId, "The reservation ended.", cancellationToken);
    }
}

// ── Housing ─────────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A viewing was agreed.
/// <para>
/// <b>The seeker is who this concerns</b> — the frontend says "I have a viewing coming up" and scopes
/// it to <c>user</c>. The landlord is not named: they arranged it, and an automation reminding them of
/// every viewing on their own diary is a different feature nobody asked for.
/// </para>
/// <para>
/// The contract carries no address and this carries none either. A viewing reminder says a viewing is
/// coming; where it is stays behind the disclosure decision Housing makes with a viewer identity
/// attached, which is the rule <c>ViewingScheduledIntegrationEvent</c>'s own summary states.
/// </para>
/// </summary>
public sealed class ViewingReminderHorizonConsumer
    : SubjectHorizonConsumer<ViewingScheduledIntegrationEvent>
{
    public ViewingReminderHorizonConsumer(AutomationSubjectHorizonService horizons) : base(horizons) { }

    protected override Task HandleAsync(
        ViewingScheduledIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["eventType"] = integrationEvent.IsVirtual ? "virtual" : "inPerson";
        values["dayOfWeek"] = ((int)integrationEvent.ScheduledFor.DayOfWeek)
            .ToString(CultureInfo.InvariantCulture);
        values["timeOfDay"] = integrationEvent.ScheduledFor.ToString(
            "HH:mm", CultureInfo.InvariantCulture);

        return Horizons.ArmAsync(
            "housing.viewingReminder",
            AutomationSubjectKinds.Viewing,
            integrationEvent.ViewingId,
            integrationEvent.IsVirtual ? "A virtual viewing" : "A viewing",
            integrationEvent.ScheduledFor,
            null,
            Owners(integrationEvent.SeekerUserId),
            values,
            "You have a viewing coming up.",
            cancellationToken);
    }
}

/// <summary>A viewing happened. Nothing left to remind about.</summary>
public sealed class ViewingCompletedHorizonConsumer
    : SubjectHorizonConsumer<ViewingCompletedIntegrationEvent>
{
    public ViewingCompletedHorizonConsumer(AutomationSubjectHorizonService horizons) : base(horizons) { }

    protected override Task HandleAsync(
        ViewingCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return Horizons.CancelForSubjectAsync(
            integrationEvent.ViewingId, "The viewing is over.", cancellationToken);
    }
}
