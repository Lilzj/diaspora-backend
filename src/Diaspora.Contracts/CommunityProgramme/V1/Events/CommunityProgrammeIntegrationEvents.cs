using Diaspora.Contracts.Events;

namespace Diaspora.Contracts.CommunityProgramme.V1.Events;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// B18's published contracts.
//
// Flat primitives and strings, so a consumer parses one without referencing a single Community
// Programme type — which is what makes these safe to depend on: nothing here can be changed by a
// refactor inside an aggregate.
//
// **No payload declares a property called EventId.** The base IntegrationEvent already owns that
// name, and an identifier shadowing the de-duplication key would silently break every consumer's
// idempotency. B12 recorded that trap; these follow the same rule, and the subject's own identifier
// is spelled out in full throughout.
//
// **No payload carries a recipient list.** An organization announcement reaches its members and an
// expiring membership reaches its holder; the first is resolved by the consumer from the scope, and
// the second names one member because there is exactly one. A contract that carried every member of
// an organization would grow without bound and would put a roll on a bus every module can read —
// B12's own reason for publishing AffectedAttendeeCount rather than a guest list.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// A programme membership reached a state its holder needs to know about.
/// <para>
/// One contract for four facts — approved, rejected, renewal due, expired — because they differ only
/// in the status they carry and a consumer switching on one field is simpler than four consumers that
/// must not disagree. The relay publishes it for those four transitions and no others: a move to
/// <c>Draft</c> or <c>PaymentPending</c> is bookkeeping the member is already watching on screen.
/// </para>
/// </summary>
public sealed record ProgrammeMembershipStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid MembershipId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid MemberUserId { get; init; }

    public required Guid TierId { get; init; }

    /// <summary>One of the fourteen <c>ProgrammeMembershipStatus</c> names.</summary>
    public required string PreviousStatus { get; init; }

    public required string CurrentStatus { get; init; }

    /// <summary><c>Member</c>, <c>Organization</c> or <c>System</c>.</summary>
    public required string Actor { get; init; }

    /// <summary>The organization's own words, when it gave any. Surfaced to the member verbatim.</summary>
    public string? Reason { get; init; }

    /// <summary>When the membership lapses, when it has a date. Null on a lifetime membership.</summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>
/// A volunteer application was decided.
/// <para>
/// The decision only, and never the submission: the organizer already knows somebody applied — they
/// are looking at the queue — and the applicant is the one who needs telling.
/// </para>
/// </summary>
public sealed record VolunteerApplicationDecidedIntegrationEvent : IntegrationEvent
{
    public required Guid ApplicationId { get; init; }

    public required Guid OpportunityId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid VolunteerUserId { get; init; }

    /// <summary><c>Approved</c>, <c>Rejected</c> or <c>Waitlisted</c>.</summary>
    public required string Decision { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

/// <summary>An approved volunteer was placed on a shift.</summary>
public sealed record VolunteerAssignmentCreatedIntegrationEvent : IntegrationEvent
{
    public required Guid AssignmentId { get; init; }

    public required Guid OpportunityId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid VolunteerUserId { get; init; }

    public Guid? ShiftId { get; init; }

    /// <summary>When the volunteer is expected. What a reminder is scheduled against.</summary>
    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// An organizer confirmed a volunteer's hours.
/// <para>
/// <b>Confirmed hours only.</b> A self-reported hour is not a fact about the world, and nothing
/// downstream — a profile, an impact figure, a certificate — may count one.
/// </para>
/// </summary>
public sealed record VolunteerHoursConfirmedIntegrationEvent : IntegrationEvent
{
    public required Guid AssignmentId { get; init; }

    public required Guid OpportunityId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required Guid VolunteerUserId { get; init; }

    public required decimal Hours { get; init; }

    public required DateTimeOffset ConfirmedAt { get; init; }
}

/// <summary>
/// A poll opened, closed, or had its results published.
/// <para>
/// The three transitions somebody outside the module can act on. A draft becoming scheduled is
/// bookkeeping.
/// </para>
/// </summary>
public sealed record CommunityPollStatusChangedIntegrationEvent : IntegrationEvent
{
    public required Guid PollId { get; init; }

    public Guid? OrganizationId { get; init; }

    /// <summary><c>Public</c>, <c>OrganizationMembers</c>, <c>Neighborhood</c>, <c>Country</c>, <c>Invited</c>.</summary>
    public required string Audience { get; init; }

    public required string PreviousStatus { get; init; }

    public required string CurrentStatus { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>
/// An announcement was published to a scope.
/// <para>
/// <b>The scope, not the audience.</b> The consumer resolves who that reaches, which keeps the
/// contract a constant size however many members an organization has.
/// </para>
/// </summary>
public sealed record CommunityAnnouncementPublishedIntegrationEvent : IntegrationEvent
{
    public required Guid AnnouncementId { get; init; }

    /// <summary><c>Community</c>, <c>Organization</c>, <c>Neighborhood</c>, <c>Event</c>, <c>Campaign</c>.</summary>
    public required string Scope { get; init; }

    /// <summary>What the scope names. Null only on a country-wide community announcement.</summary>
    public Guid? ScopeId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required string Title { get; init; }

    public required Guid PublishedByUserId { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}

/// <summary>
/// A community reached a size it had never reached before.
/// <para>
/// <b>The fourth event the Backend Gap Build Order called genuinely missing, and the last.</b> It
/// waited on two things, not one: a contract, and somebody an automation about a community could
/// belong to. Communities had members and nobody who ran them until 11 Sep 2026, when founding a
/// community began making its founder the owner.
/// </para>
/// <para>
/// <b>Published once per milestone, ever.</b> <c>Community.HighestMemberMilestone</c> only rises,
/// so a community that dips below 100 and comes back has not reached 100 twice.
/// </para>
/// <para>
/// The name travels with it because the one sentence anybody will read — "Nigerians in Leeds
/// reached 100 members" — needs it, and a consumer that had to look it up would be reading
/// another module's table to write a notice.
/// </para>
/// </summary>
public sealed record CommunityMilestoneReachedIntegrationEvent : IntegrationEvent
{
    public required Guid CommunityId { get; init; }

    public required string CommunityName { get; init; }

    public required int Milestone { get; init; }

    public required int MemberCount { get; init; }

    public required DateTimeOffset ReachedAt { get; init; }
}
