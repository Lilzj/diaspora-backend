using Diaspora.Contracts.CommunityProgramme.V1.Events;
using Diaspora.Modules.Notifications.Application.Dispatch;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Modules.Notifications.Application.Consumers.CommunityProgramme;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// B18's facts, turned into messages.
//
// **B13.3 left eight of the Phase 12 register's sixteen notification types waiting "on a subject the
// platform does not have".** B18 supplies four of those subjects — a programme membership, a
// volunteer application, a volunteer assignment and a poll — and these consumers are what makes the
// waiting types real rather than nominal. The remaining four wait on a fundraising campaign, which
// is B15.1's.
//
// Every consumer here is the same fifteen boring lines: read a published payload, name the person to
// be told, hand a trigger to the dispatcher. None of them names a template, a category, a priority or
// a channel — those are rules, and rules are reference data seeded by the contributors beside this
// file. That separation is what lets a deployment change the wording of a rejection without a
// release.
//
// **None of them names the person who acted, either.** "The organisation has decided" rather than
// "Amina has decided": the contracts carry identifiers and not display names, and resolving one would
// mean this module querying Identity for a name to put in a push banner.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A programme membership reached one of the four states its holder needs to know about.
/// <para>
/// One consumer for four facts, because they differ only in the status they carry — and B18's relay
/// already decided which four are worth publishing. Approved, rejected, renewal due and expired reach
/// the member; a move to <c>Draft</c> or <c>PaymentPending</c> never leaves the module.
/// </para>
/// <para>
/// <b>The status picks the rule through the audience.</b> Four audiences on one event name means four
/// rules with four templates and four priorities — a rejection is not urgent and an expiry is — and
/// the alternative, four contracts, would have made B18's relay four times the size for no gain.
/// </para>
/// </summary>
public sealed class ProgrammeMembershipStatusChangedConsumer
    : ModuleEventConsumer<ProgrammeMembershipStatusChangedIntegrationEvent>
{
    public ProgrammeMembershipStatusChangedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => "CommunityProgramme";

    protected override IEnumerable<NotificationTrigger> Translate(
        ProgrammeMembershipStatusChangedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var audience = integrationEvent.CurrentStatus switch
        {
            "Approved" => "membership-approved",
            "Rejected" => "membership-rejected",
            "RenewalDue" => "membership-renewal-due",
            "Expired" => "membership-expired",
            _ => null
        };

        if (audience is null)
        {
            // A status B18 published and no rule addresses. Nothing happens, which is the normal
            // outcome for a consumer that has nothing to say.
            yield break;
        }

        var values = Values();
        values["membershipid"] = integrationEvent.MembershipId;
        values["organizationid"] = integrationEvent.OrganizationId;

        // The organisation's own words, when it gave any. Surfaced verbatim: a member refused by a
        // body is owed the sentence it wrote rather than a code.
        if (!string.IsNullOrWhiteSpace(integrationEvent.Reason))
        {
            values["reason"] = integrationEvent.Reason;
        }

        yield return Trigger(
            UserId.From(integrationEvent.MemberUserId),
            audience,
            integrationEvent.ChangedAt,
            values,
            subjectType: "membership",
            subjectId: integrationEvent.MembershipId.ToString(),
            subjectMoment: integrationEvent.ExpiresAt);
    }
}

/// <summary>
/// A volunteer application was decided.
/// <para>
/// The applicant, and only the applicant. The organiser made the decision and is looking at the queue
/// they made it in.
/// </para>
/// </summary>
public sealed class VolunteerApplicationDecidedConsumer
    : ModuleEventConsumer<VolunteerApplicationDecidedIntegrationEvent>
{
    public VolunteerApplicationDecidedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => "CommunityProgramme";

    protected override IEnumerable<NotificationTrigger> Translate(
        VolunteerApplicationDecidedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["applicationid"] = integrationEvent.ApplicationId;
        values["opportunityid"] = integrationEvent.OpportunityId;
        values["decision"] = integrationEvent.Decision;

        if (!string.IsNullOrWhiteSpace(integrationEvent.Reason))
        {
            values["reason"] = integrationEvent.Reason;
        }

        yield return Trigger(
            UserId.From(integrationEvent.VolunteerUserId),
            "volunteer",
            integrationEvent.DecidedAt,
            values,
            subjectType: "volunteer-application",
            subjectId: integrationEvent.ApplicationId.ToString());
    }
}

/// <summary>
/// An approved volunteer was placed on a shift.
/// <para>
/// <b>The moment is carried, and that is what makes a reminder possible later.</b> <c>SubjectMoment</c>
/// is the shift's start; a digest or a reminder rule keyed on it does not need to ask B18 when the
/// volunteer is expected.
/// </para>
/// </summary>
public sealed class VolunteerAssignmentCreatedConsumer
    : ModuleEventConsumer<VolunteerAssignmentCreatedIntegrationEvent>
{
    public VolunteerAssignmentCreatedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => "CommunityProgramme";

    protected override IEnumerable<NotificationTrigger> Translate(
        VolunteerAssignmentCreatedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["assignmentid"] = integrationEvent.AssignmentId;
        values["opportunityid"] = integrationEvent.OpportunityId;

        yield return Trigger(
            UserId.From(integrationEvent.VolunteerUserId),
            "volunteer",
            integrationEvent.CreatedAt,
            values,
            subjectType: "volunteer-assignment",
            subjectId: integrationEvent.AssignmentId.ToString(),
            subjectMoment: integrationEvent.StartsAt);
    }
}

/// <summary>
/// An organiser confirmed a volunteer's hours.
/// <para>
/// A receipt, and low urgency by rule: it interrupts nobody, and the volunteer already knows they did
/// the work. What it is worth telling them is that somebody stood behind it — which is what turns the
/// hours into something they can show.
/// </para>
/// </summary>
public sealed class VolunteerHoursConfirmedConsumer
    : ModuleEventConsumer<VolunteerHoursConfirmedIntegrationEvent>
{
    public VolunteerHoursConfirmedConsumer(INotificationDispatcher dispatcher)
        : base(dispatcher) { }

    protected override string SourceModule => "CommunityProgramme";

    protected override IEnumerable<NotificationTrigger> Translate(
        VolunteerHoursConfirmedIntegrationEvent integrationEvent)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var values = Values();
        values["assignmentid"] = integrationEvent.AssignmentId;
        values["hours"] = integrationEvent.Hours;

        yield return Trigger(
            UserId.From(integrationEvent.VolunteerUserId),
            "volunteer",
            integrationEvent.ConfirmedAt,
            values,
            subjectType: "volunteer-assignment",
            subjectId: integrationEvent.AssignmentId.ToString());
    }
}
