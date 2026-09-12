using Diaspora.Contracts.CommunityProgramme.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.CommunityProgramme;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// Closing a community, and handing one over.
//
// **Four contracts, and every one of them answers a question somebody is waiting on.** A community's
// owner asks Diaspora to close it and hears nothing until an administrator works through a queue; an
// owner asks a moderator to take the community on and hears nothing until that person opens the app.
// Before these consumers the answer existed and only the screen knew it.
//
// Two of the four carry two outcomes each, and the audience is picked from the outcome rather than
// from a template name — an approval and a refusal are different messages to somebody in a different
// position, and only one of them has a deadline. That is the same shape the membership consumer
// beside this file uses, and for the same reason.
//
// **The slug travels as a value because the route needs it.** These messages link to a community
// screen, which the client addresses by slug; the identifier goes on the subject instead, where a
// later rule can key on it without parsing a route.
//
// None of them names anybody. "The person you asked" and "the owner of this community" rather than
// a display name: the contracts carry identifiers, and resolving one would mean this module querying
// Identity for a name to put in an email.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A Diaspora administrator decided what happens to a community its owner asked to close.
/// <para>
/// The owner, either way, and the two messages are not the same one. An approval names the day the
/// community goes and the fact that the owner can still stop it; a refusal carries the
/// administrator's own sentence and points at the other way out — handing the community to somebody
/// who already helps run it.
/// </para>
/// <para>
/// <b>The moment is carried only when there is going to be one.</b> <c>EffectiveAt</c> is a closure
/// date, and on a refusal there is no closure — attaching it anyway would leave a moment on the
/// subject that a reminder rule could later act on for a community that is staying open.
/// </para>
/// </summary>
public sealed class CommunityDeletionDecidedConsumer
    : ModuleEventConsumer<CommunityDeletionDecidedIntegrationEvent>
{
    public CommunityDeletionDecidedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => "CommunityProgramme";

    protected override IEnumerable<NotificationTrigger> Translate(
        CommunityDeletionDecidedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["communityname"] = integrationEvent.CommunityName;
        values["communityslug"] = integrationEvent.CommunitySlug;
        values["effectiveat"] = integrationEvent.EffectiveAt;

        // Diaspora's own words, when it gave any. Surfaced verbatim: an owner refused is owed the
        // sentence somebody wrote rather than this module's summary of it.
        if (!string.IsNullOrWhiteSpace(integrationEvent.DecisionReason))
        {
            values["reason"] = integrationEvent.DecisionReason;
        }

        yield return Trigger(
            UserId.From(integrationEvent.OwnerUserId),
            integrationEvent.Approved ? "closure-approved" : "closure-refused",
            integrationEvent.OccurredAt,
            values,
            subjectType: "community",
            subjectId: integrationEvent.CommunityId.ToString(),
            subjectMoment: integrationEvent.Approved ? integrationEvent.EffectiveAt : null);
    }
}

/// <summary>
/// The wait ran out on an approved request and the community is closed.
/// <para>
/// The owner who asked for it. Nobody else is told here — the members lost the community when it
/// closed and B18 decides what, if anything, they hear about that.
/// </para>
/// </summary>
public sealed class CommunityDeletedConsumer
    : ModuleEventConsumer<CommunityDeletedIntegrationEvent>
{
    public CommunityDeletedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => "CommunityProgramme";

    protected override IEnumerable<NotificationTrigger> Translate(
        CommunityDeletedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["communityname"] = integrationEvent.CommunityName;
        values["communityslug"] = integrationEvent.CommunitySlug;

        yield return Trigger(
            UserId.From(integrationEvent.OwnerUserId),
            "owner",
            integrationEvent.ClosedAt,
            values,
            subjectType: "community",
            subjectId: integrationEvent.CommunityId.ToString());
    }
}

/// <summary>
/// An owner asked somebody who helps run their community to take it over.
/// <para>
/// The person being asked, and only them — the owner is looking at the screen they made the offer
/// on. They have something to do and a date by which to do it, which is why the offer's expiry is
/// both a value the message reads and the subject's moment.
/// </para>
/// <para>
/// <b>The owner's covering note is not carried.</b> It is a message from a person this module will
/// not name, and it belongs on the screen the deep link opens rather than under a heading the
/// reader cannot attribute.
/// </para>
/// </summary>
public sealed class CommunityOwnershipOfferedConsumer
    : ModuleEventConsumer<CommunityOwnershipOfferedIntegrationEvent>
{
    public CommunityOwnershipOfferedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => "CommunityProgramme";

    protected override IEnumerable<NotificationTrigger> Translate(
        CommunityOwnershipOfferedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["communityname"] = integrationEvent.CommunityName;
        values["communityslug"] = integrationEvent.CommunitySlug;
        values["expiresat"] = integrationEvent.ExpiresAt;

        yield return Trigger(
            UserId.From(integrationEvent.OfferedToUserId),
            "successor",
            integrationEvent.OccurredAt,
            values,
            subjectType: "community-transfer",
            subjectId: integrationEvent.TransferId.ToString(),
            subjectMoment: integrationEvent.ExpiresAt);
    }
}

/// <summary>
/// The person asked has answered.
/// <para>
/// The owner who asked, either way. On a yes they are no longer the owner and the message says so;
/// on a no nothing changed, and what they need is the other person's reason and the fact that they
/// can ask somebody else.
/// </para>
/// </summary>
public sealed class CommunityOwnershipDecidedConsumer
    : ModuleEventConsumer<CommunityOwnershipDecidedIntegrationEvent>
{
    public CommunityOwnershipDecidedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => "CommunityProgramme";

    protected override IEnumerable<NotificationTrigger> Translate(
        CommunityOwnershipDecidedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["communityname"] = integrationEvent.CommunityName;
        values["communityslug"] = integrationEvent.CommunitySlug;

        // Their words, when they gave any, and quoted rather than paraphrased.
        if (!string.IsNullOrWhiteSpace(integrationEvent.DeclineReason))
        {
            values["reason"] = integrationEvent.DeclineReason;
        }

        yield return Trigger(
            UserId.From(integrationEvent.OfferedByUserId),
            integrationEvent.Accepted ? "ownership-accepted" : "ownership-declined",
            integrationEvent.OccurredAt,
            values,
            subjectType: "community-transfer",
            subjectId: integrationEvent.TransferId.ToString());
    }
}
