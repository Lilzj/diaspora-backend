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
