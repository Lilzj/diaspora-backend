using Diaspora.Modules.CommunityProgramme.Domain.Announcements;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.Modules.CommunityProgramme.Domain.Memberships;
using Diaspora.Modules.CommunityProgramme.Domain.Polls;
using Diaspora.Modules.CommunityProgramme.Domain.Volunteering;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;

namespace Diaspora.Modules.CommunityProgramme.Domain.Repositories;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// The module's read and write surfaces.
//
// One interface per aggregate, each keyed on that aggregate's strongly typed identifier — the rule
// B1.3 made and DSP3001 enforces. There is deliberately no generic repository and no "get all":
// a caller that wants to iterate every membership on the platform is about to do something this
// module should be doing for it.
//
// Every lookup that could be used to build a page takes a filter and returns a CursorPagedResult,
// because an offset page over a table being written to skips and repeats rows — and on a
// membership roll the row somebody never saw is a person.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>How the module reads and writes membership programmes.</summary>
public interface IMembershipProgrammeRepository
{
    Task<MembershipProgramme?> GetAsync(
        MembershipProgrammeId id, CancellationToken cancellationToken = default);

    /// <summary>The programme an organization runs, or null when it runs none.</summary>
    Task<MembershipProgramme?> FindByOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Several at once. The N+1 guard for a discovery page.</summary>
    Task<IReadOnlyDictionary<Guid, MembershipProgramme>> FindManyByOrganizationAsync(
        IReadOnlyCollection<Guid> organizationIds, CancellationToken cancellationToken = default);

    /// <summary>
    /// The discovery page.
    /// <para>
    /// <b>Visibility is applied in the query, not after it.</b> A private programme is absent from
    /// the result set rather than filtered from it, which is the difference between "the client did
    /// not draw it" and "the device never had it".
    /// </para>
    /// </summary>
    Task<CursorPagedResult<MembershipProgramme>> SearchAsync(
        ProgrammeQueryFilter filter, CancellationToken cancellationToken = default);

    void Add(MembershipProgramme programme);
}

/// <summary>How the module reads and writes programme memberships.</summary>
public interface IProgrammeMembershipRepository
{
    Task<ProgrammeMembership?> GetAsync(
        ProgrammeMembershipId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The membership as it stands in the database, ignoring anything this request already tracked.
    /// <para>
    /// <b><see cref="GetAsync"/> resolves through the change tracker's identity map, which is
    /// normally what a write path wants and is exactly wrong here.</b> Where another request has
    /// moved the row since this one loaded it, a second <c>GetAsync</c> hands back the stale instance
    /// — so a "reload" reloads nothing and the response reports a status that is no longer true. B15.1
    /// found this by racing two membership payment bindings: the loser reported <c>Approved</c> for a
    /// membership the winner had already made <c>Active</c>.
    /// </para>
    /// <para>
    /// Read-only, and deliberately: an untracked entity cannot be saved, which is what stops this
    /// being reached for on a write path by mistake.
    /// </para>
    /// </summary>
    Task<ProgrammeMembership?> ReadAsync(
        ProgrammeMembershipId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// The membership joining one member to one organization's programme, whatever its state.
    /// <para>
    /// Returns terminal memberships too, and that is load-bearing: somebody an organization removed
    /// must not be able to rejoin by creating a second live row, and the only way to know is to look
    /// at the one that already exists.
    /// </para>
    /// </summary>
    Task<ProgrammeMembership?> FindCurrentAsync(
        Guid organizationId, UserId memberUserId, CancellationToken cancellationToken = default);

    /// <summary>Every membership the caller holds, terminal ones included. Bounded.</summary>
    Task<IReadOnlyList<ProgrammeMembership>> FindForMemberAsync(
        UserId memberUserId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>The organization's member roll, paged and filterable.</summary>
    Task<CursorPagedResult<ProgrammeMembership>> SearchAsync(
        MembershipRollFilter filter, CancellationToken cancellationToken = default);

    /// <summary>B15's idempotency lookup, for the application. Replayed on a repeat.</summary>
    Task<ProgrammeMembership?> FindByApplicationIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>B15's idempotency lookup, for a renewal.</summary>
    Task<ProgrammeMembership?> FindByRenewalIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>How many memberships hold each tier, counting only those that occupy a place.</summary>
    Task<IReadOnlyDictionary<MembershipTierId, int>> CountByTierAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>How many memberships an organization has in each state.</summary>
    Task<IReadOnlyDictionary<ProgrammeMembershipStatus, int>> CountByStatusAsync(
        Guid organizationId, CancellationToken cancellationToken = default);

    /// <summary>Memberships whose renewal window has opened. The sweep's work list.</summary>
    Task<IReadOnlyList<ProgrammeMembership>> FindDueForRenewalAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Memberships past their expiry. The sweep's second work list.</summary>
    Task<IReadOnlyList<ProgrammeMembership>> FindExpiredAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Every organization with at least one membership. The reconciliation work list.</summary>
    Task<IReadOnlyList<Guid>> FindOrganizationsWithMembershipsAsync(
        int limit, CancellationToken cancellationToken = default);

    /// <summary>
    /// Memberships created in a window, for the analytics read.
    /// <para>
    /// Returns the creation moments alone rather than the aggregates, because an analytics read that
    /// materialised a year of memberships to count them would be a page nobody could open.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<DateTimeOffset>> FindJoinMomentsAsync(
        Guid organizationId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    void Add(ProgrammeMembership membership);
}

/// <summary>How the module reads and writes volunteer opportunities.</summary>
public interface IVolunteerOpportunityRepository
{
    Task<VolunteerOpportunity?> GetAsync(
        VolunteerOpportunityId id, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<VolunteerOpportunityId, VolunteerOpportunity>> FindManyAsync(
        IReadOnlyCollection<VolunteerOpportunityId> ids,
        CancellationToken cancellationToken = default);

    Task<CursorPagedResult<VolunteerOpportunity>> SearchAsync(
        OpportunityQueryFilter filter, CancellationToken cancellationToken = default);

    Task<VolunteerOpportunity?> FindByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>Published opportunities whose application deadline has passed. The sweep's list.</summary>
    Task<IReadOnlyList<VolunteerOpportunity>> FindPastDeadlineAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    Task<int> CountByStatusAsync(
        Guid organizationId,
        VolunteerOpportunityStatus status,
        CancellationToken cancellationToken = default);

    void Add(VolunteerOpportunity opportunity);
}

/// <summary>How the module reads and writes volunteer applications.</summary>
public interface IVolunteerApplicationRepository
{
    Task<VolunteerApplication?> GetAsync(
        VolunteerApplicationId id, CancellationToken cancellationToken = default);

    /// <summary>The caller's application for an opportunity, whatever its state.</summary>
    Task<VolunteerApplication?> FindForVolunteerAsync(
        VolunteerOpportunityId opportunityId,
        UserId volunteerUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VolunteerApplication>> FindForVolunteerAsync(
        UserId volunteerUserId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>The organizer's applicant queue, paged.</summary>
    Task<CursorPagedResult<VolunteerApplication>> SearchAsync(
        ApplicationQueryFilter filter, CancellationToken cancellationToken = default);

    Task<VolunteerApplication?> FindByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The waiting list in submission order, so a position can be computed rather than stored.
    /// <para>
    /// A stored position goes wrong the moment somebody ahead withdraws, and a member told they are
    /// fourth when they are second has been told something false.
    /// </para>
    /// </summary>
    Task<IReadOnlyList<VolunteerApplicationId>> FindWaitlistOrderAsync(
        VolunteerOpportunityId opportunityId, CancellationToken cancellationToken = default);

    /// <summary>The counts an opportunity's reconciliation needs, in one round trip.</summary>
    Task<OpportunityCounts> CountForOpportunityAsync(
        VolunteerOpportunityId opportunityId, CancellationToken cancellationToken = default);

    Task<int> CountPendingAsync(Guid organizationId, CancellationToken cancellationToken = default);

    void Add(VolunteerApplication application);
}

/// <summary>How the module reads and writes volunteer assignments and certificates.</summary>
public interface IVolunteerAssignmentRepository
{
    Task<VolunteerAssignment?> GetAsync(
        VolunteerAssignmentId id, CancellationToken cancellationToken = default);

    Task<VolunteerAssignment?> FindForApplicationAsync(
        VolunteerApplicationId applicationId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VolunteerAssignment>> FindForVolunteerAsync(
        UserId volunteerUserId, int limit = 50, CancellationToken cancellationToken = default);

    /// <summary>
    /// Confirmed hours only, and the interface says so in its name so a caller cannot ask for the
    /// other kind by accident.
    /// </summary>
    Task<IReadOnlyList<VolunteerAssignment>> FindConfirmedForVolunteerAsync(
        UserId volunteerUserId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<VolunteerAssignment>> FindConfirmedForOrganizationAsync(
        Guid organizationId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<VolunteerShiftId, int>> CountByShiftAsync(
        VolunteerOpportunityId opportunityId, CancellationToken cancellationToken = default);

    void Add(VolunteerAssignment assignment);

    Task<IReadOnlyList<VolunteerCertificate>> FindCertificatesForVolunteerAsync(
        UserId volunteerUserId, int limit = 50, CancellationToken cancellationToken = default);

    Task<VolunteerCertificate?> FindCertificateForAssignmentAsync(
        VolunteerAssignmentId assignmentId, CancellationToken cancellationToken = default);

    void AddCertificate(VolunteerCertificate certificate);
}

/// <summary>How the module reads and writes polls and their responses.</summary>
public interface ICommunityPollRepository
{
    Task<CommunityPoll?> GetAsync(
        CommunityPollId id, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<CommunityPoll>> SearchAsync(
        PollQueryFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Scheduled polls whose opening moment has arrived. The sweep's list.</summary>
    Task<IReadOnlyList<CommunityPoll>> FindDueToOpenAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    /// <summary>Open polls whose closing moment has passed. The sweep's second list.</summary>
    Task<IReadOnlyList<CommunityPoll>> FindDueToCloseAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default);

    Task<int> CountOpenAsync(Guid organizationId, CancellationToken cancellationToken = default);

    void Add(CommunityPoll poll);

    // ── Responses ───────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The caller's own response, found by the poll-scoped fingerprint.
    /// <para>
    /// By fingerprint rather than by user, so the same lookup works on an anonymous poll — where
    /// there is no stored user to look one up by.
    /// </para>
    /// </summary>
    Task<PollResponse?> FindResponseAsync(
        CommunityPollId pollId, string voterFingerprint, CancellationToken cancellationToken = default);

    Task<PollResponse?> FindResponseByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// The tally, computed in the database.
    /// <para>
    /// <b>The one query in the module that must never be done in memory.</b> Loading every response
    /// to count them is the shape that works on a poll with four answers and falls over on the one
    /// that matters.
    /// </para>
    /// </summary>
    Task<IReadOnlyDictionary<PollOptionId, int>> TallyAsync(
        CommunityPollId pollId, CancellationToken cancellationToken = default);

    Task<int> CountResponsesAsync(
        CommunityPollId pollId, CancellationToken cancellationToken = default);

    /// <summary>The free-text answers on an open-response poll. Bounded.</summary>
    Task<IReadOnlyList<string>> ListOpenResponsesAsync(
        CommunityPollId pollId, int limit = 200, CancellationToken cancellationToken = default);

    /// <summary>Response moments in a window, for the participation series.</summary>
    Task<IReadOnlyList<DateTimeOffset>> FindResponseMomentsAsync(
        Guid organizationId,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);

    /// <summary>How many polls the caller has answered. One of the impact figures.</summary>
    Task<int> CountResponsesForVoterAsync(
        UserId voterUserId,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken cancellationToken = default);

    void AddResponse(PollResponse response);
}

/// <summary>How the module reads and writes announcements.</summary>
public interface ICommunityAnnouncementRepository
{
    Task<CommunityAnnouncement?> GetAsync(
        CommunityAnnouncementId id, CancellationToken cancellationToken = default);

    Task<CursorPagedResult<CommunityAnnouncement>> SearchAsync(
        AnnouncementQueryFilter filter, CancellationToken cancellationToken = default);

    Task<CommunityAnnouncement?> FindByIdempotencyKeyAsync(
        string idempotencyKey, CancellationToken cancellationToken = default);

    Task<int> CountSinceAsync(
        Guid organizationId, DateTimeOffset since, CancellationToken cancellationToken = default);

    void Add(CommunityAnnouncement announcement);
}

/// <summary>The counts an opportunity's reconciliation needs, gathered in one round trip.</summary>
public sealed record OpportunityCounts
{
    public required int Filled { get; init; }

    public required int Waiting { get; init; }

    public required int Pending { get; init; }
}
