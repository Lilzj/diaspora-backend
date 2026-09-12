using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Primitives;

namespace Diaspora.Modules.CommunityProgramme.Domain.Events;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// B18's domain events.
//
// Private to this module, carrying its own identifiers and enums. Each is settled by exactly one
// handler here — DSP1002 reports a second — and the relay is what decides which of them becomes a
// published fact other modules may consume.
//
// One event per journey rather than one per state. A membership has fourteen states and four of
// them matter to somebody outside this module; raising fourteen event types would mean the relay
// grew a handler per state and the aggregate grew a method per state, and neither would make the
// fourteenth easier to add than the first.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Raised on every programme-membership transition.
/// <para>
/// The relay publishes four of them — approved, rejected, renewal due and expired — because those
/// are the four the member has to be told about. A move to <c>Draft</c> or <c>PaymentPending</c> is
/// bookkeeping the member is already watching on screen.
/// </para>
/// </summary>
public sealed record ProgrammeMembershipStatusChangedDomainEvent : DomainEvent
{
    public required ProgrammeMembershipId MembershipId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required MembershipTierId TierId { get; init; }

    public required UserId MemberUserId { get; init; }

    public required ProgrammeMembershipStatus PreviousStatus { get; init; }

    public required ProgrammeMembershipStatus CurrentStatus { get; init; }

    public required MembershipActor Actor { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>Raised when an opportunity moves between lifecycle states.</summary>
public sealed record VolunteerOpportunityStatusChangedDomainEvent : DomainEvent
{
    public required VolunteerOpportunityId OpportunityId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required VolunteerOpportunityStatus PreviousStatus { get; init; }

    public required VolunteerOpportunityStatus CurrentStatus { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>
/// Raised when a volunteer application is decided.
/// <para>
/// Only the decision, not the submission. The organizer already knows somebody applied — they are
/// looking at the queue — and the applicant is the one who needs telling.
/// </para>
/// </summary>
public sealed record VolunteerApplicationDecidedDomainEvent : DomainEvent
{
    public required VolunteerApplicationId ApplicationId { get; init; }

    public required VolunteerOpportunityId OpportunityId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required UserId VolunteerUserId { get; init; }

    public required VolunteerApplicationStatus PreviousStatus { get; init; }

    public required VolunteerApplicationStatus CurrentStatus { get; init; }

    public string? Reason { get; init; }

    public required DateTimeOffset DecidedAt { get; init; }
}

/// <summary>Raised when an approved volunteer is placed on a shift.</summary>
public sealed record VolunteerAssignmentCreatedDomainEvent : DomainEvent
{
    public required VolunteerAssignmentId AssignmentId { get; init; }

    public required VolunteerApplicationId ApplicationId { get; init; }

    public required VolunteerOpportunityId OpportunityId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required UserId VolunteerUserId { get; init; }

    public VolunteerShiftId? ShiftId { get; init; }

    public required DateTimeOffset StartsAt { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>
/// Raised when an organizer confirms hours.
/// <para>
/// <b>Confirmed hours only, and that is the point of the event.</b> A self-reported hour is not a
/// fact about the world, and nothing downstream — a profile, an impact figure, a certificate — is
/// permitted to count one.
/// </para>
/// </summary>
public sealed record VolunteerHoursConfirmedDomainEvent : DomainEvent
{
    public required VolunteerAssignmentId AssignmentId { get; init; }

    public required VolunteerOpportunityId OpportunityId { get; init; }

    public required Guid OrganizationId { get; init; }

    public required UserId VolunteerUserId { get; init; }

    public required decimal Hours { get; init; }

    public required UserId ConfirmedBy { get; init; }

    public required DateTimeOffset ConfirmedAt { get; init; }
}

/// <summary>Raised when a poll moves between lifecycle states.</summary>
public sealed record PollStatusChangedDomainEvent : DomainEvent
{
    public required CommunityPollId PollId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required PollAudience Audience { get; init; }

    public required PollStatus PreviousStatus { get; init; }

    public required PollStatus CurrentStatus { get; init; }

    public required DateTimeOffset ChangedAt { get; init; }
}

/// <summary>
/// Raised when an announcement is published.
/// <para>
/// The one B18 event whose whole purpose is fan-out: B13 resolves the audience from the scope and
/// delivers. This module names the scope and never a recipient list — a contract carrying every
/// member of an organization would grow without bound and would put the roll on a bus every module
/// can read.
/// </para>
/// </summary>
public sealed record CommunityAnnouncementPublishedDomainEvent : DomainEvent
{
    public required CommunityAnnouncementId AnnouncementId { get; init; }

    public required AnnouncementScope Scope { get; init; }

    public Guid? ScopeId { get; init; }

    public Guid? OrganizationId { get; init; }

    public required string Title { get; init; }

    public required UserId PublishedBy { get; init; }

    public required DateTimeOffset PublishedAt { get; init; }
}

/// <summary>
/// A community reached a size it had never reached before.
/// <para>
/// Raised by <c>Community.RecordMemberJoined</c>, once per milestone, ever — see
/// <c>Community.HighestMemberMilestone</c>. The count rides with the milestone because a community
/// that jumped from 9 to 11 in one save reached 10 on the way and the figure should say where it is.
/// </para>
/// </summary>
public sealed record CommunityMilestoneReachedDomainEvent : DomainEvent
{
    public required CommunityId CommunityId { get; init; }

    public required string CommunityName { get; init; }

    public required int Milestone { get; init; }

    public required int MemberCount { get; init; }
}

// ── Closing a community, and handing one over — 11 Sep 2026 ─────────────────────────────────
//
// Until now a community's lifecycle raised nothing at all: it was opened, suspended and archived in
// silence, because nobody outside the module needed to hear. Closing one is different — the people
// in it have to be told, and the owner has to be told what was decided about the request they made.

/// <summary>An owner has asked Diaspora to close their community. Nothing has happened to it yet.</summary>
public sealed record CommunityDeletionRequestedDomainEvent : DomainEvent
{
    public required CommunityDeletionRequestId RequestId { get; init; }

    public required CommunityId CommunityId { get; init; }

    public required string CommunityName { get; init; }

    public required UserId RequestedBy { get; init; }

    /// <summary>The owner's words. Read by the administrator who decides.</summary>
    public required string Reason { get; init; }

    /// <summary>The earliest it could be carried out.</summary>
    public required DateTimeOffset EffectiveAt { get; init; }
}

/// <summary>
/// A Diaspora administrator has decided. The owner is told either way, and on a refusal the reason
/// is the whole point of the message.
/// </summary>
public sealed record CommunityDeletionDecidedDomainEvent : DomainEvent
{
    public required CommunityDeletionRequestId RequestId { get; init; }

    public required CommunityId CommunityId { get; init; }

    public required string CommunityName { get; init; }

    /// <summary>The owner who asked, who is the person told.</summary>
    public required UserId OwnerUserId { get; init; }

    public required bool Approved { get; init; }

    /// <summary>Required on a refusal, optional on an approval. Shown verbatim.</summary>
    public string? DecisionReason { get; init; }

    public required DateTimeOffset EffectiveAt { get; init; }
}

/// <summary>The wait ran out on an approved request and the community is closed.</summary>
public sealed record CommunityDeletedDomainEvent : DomainEvent
{
    public required CommunityId CommunityId { get; init; }

    public required string CommunityName { get; init; }

    public required UserId ClosedFor { get; init; }

    public string? Reason { get; init; }
}

/// <summary>An owner has asked somebody who helps run the community to take it over.</summary>
public sealed record CommunityOwnershipOfferedDomainEvent : DomainEvent
{
    public required CommunityOwnershipTransferId TransferId { get; init; }

    public required CommunityId CommunityId { get; init; }

    public required string CommunityName { get; init; }

    /// <summary>So the notification can link to the community by handle.</summary>
    public required string CommunitySlug { get; init; }

    public required UserId OfferedBy { get; init; }

    public required UserId OfferedTo { get; init; }

    public string? Message { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

/// <summary>The person asked has answered. The owner who asked is told either way.</summary>
public sealed record CommunityOwnershipDecidedDomainEvent : DomainEvent
{
    public required CommunityOwnershipTransferId TransferId { get; init; }

    public required CommunityId CommunityId { get; init; }

    public required string CommunityName { get; init; }

    public required UserId OfferedBy { get; init; }

    public required UserId OfferedTo { get; init; }

    public required bool Accepted { get; init; }

    public string? DeclineReason { get; init; }
}
