using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityProgramme.Application.Mapping;
using Diaspora.Modules.CommunityProgramme.Application.Services;
using Diaspora.Modules.CommunityProgramme.Contracts.V1.Responses;
using Diaspora.Modules.CommunityProgramme.Domain.Capabilities;
using Diaspora.Modules.CommunityProgramme.Domain.Enums;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Repositories;
using Diaspora.Modules.CommunityProgramme.Options;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.CommunityProgramme.Application.Organizations;

// ════════════════════════════════════════════════════════════════════════════════════════════════
// COMMUNITY-LIFE-GAP-013 — organization administration, roles and permissions.
//
// The register called this "the keystone gap" and recorded that "there is no membership edge between
// a user and an organisation at all". That was not true of the certified backend: B12's
// OrganizationMembership has carried a (member, organization, role) triple with a six-rung ladder
// since it shipped, and IOrganizationDirectory.CanActOnBehalfAsync has answered authorization
// questions against it for as long.
//
// What was genuinely missing is the vocabulary above it. A role is not a capability set: nothing
// could answer "may this person approve members" without every caller reimplementing a comparison
// against B12's enum, which is exactly what five modules disagreeing about what an administrator may
// do looks like. These three reads are that vocabulary, and OrganizationCapabilityMap is the one
// place a role becomes one.
// ════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// The caller's organizations, with the role and the capability set for each.
/// <para>
/// One call rather than one per organization: the dashboard picker draws a row per body and each row
/// needs the capabilities to decide what it links to, and asking separately would be N round trips
/// through B12 for a screen that is a list.
/// </para>
/// </summary>
[RequirePermission(CommunityProgrammePermissions.Read)]
public sealed record ListMyOrganizationsQuery : IQuery<IReadOnlyList<MyOrganizationResponse>>;

public sealed class ListMyOrganizationsQueryHandler
    : IQueryHandler<ListMyOrganizationsQuery, IReadOnlyList<MyOrganizationResponse>>
{
    private readonly CommunityProgrammeAccessService _access;

    public ListMyOrganizationsQueryHandler(CommunityProgrammeAccessService access) =>
        _access = access;

    public async Task<Result<IReadOnlyList<MyOrganizationResponse>>> Handle(
        ListMyOrganizationsQuery query, CancellationToken cancellationToken)
    {
        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<MyOrganizationResponse>>(caller.Error);
        }

        var memberships = await _access.MyMembershipsAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return Result.Success<IReadOnlyList<MyOrganizationResponse>>([]);
        }

        var organizations = await _access.FindOrganizationsAsync(
            [.. memberships.Select(membership => membership.OrganizationId)], cancellationToken);

        var rows = new List<MyOrganizationResponse>();

        foreach (var membership in memberships)
        {
            // An organization that does not resolve — archived, or made undiscoverable — is skipped
            // rather than rendered as a blank card. The member's role in it survives in B12; what
            // this list is for is choosing something to administer.
            if (!organizations.TryGetValue(membership.OrganizationId, out var organization))
            {
                continue;
            }

            rows.Add(new MyOrganizationResponse
            {
                OrganizationId = membership.OrganizationId,
                Name = organization.Name,
                LogoMediaId = organization.LogoMediaId,
                Role = membership.Role,
                MembershipId = membership.MembershipId,
                IsVerified = organization.IsVerified,
                Capabilities = CommunityProgrammeMapper.ToResponse(
                    OrganizationCapabilityMap.For(membership.OrganizationId, membership.Role))
            });
        }

        return Result.Success<IReadOnlyList<MyOrganizationResponse>>(rows);
    }
}

/// <summary>
/// What the caller may do inside one organization.
/// <para>
/// <b>Served as data, never compiled into a client.</b> The frontend asserts exhaustively that it
/// never derives a capability from a role name; this is what it renders instead, and every gated
/// route re-decides the same question rather than trusting what was drawn.
/// </para>
/// <para>
/// Answers for any organization the caller can name, including one they hold no role in — where every
/// flag is false. A 404 would tell a caller which organizations exist; an all-false set tells them
/// what they can do, which is what they asked.
/// </para>
/// </summary>
[RequirePermission(CommunityProgrammePermissions.Read)]
public sealed record GetOrganizationCapabilitiesQuery : IQuery<OrganizationCapabilitiesResponse>
{
    public required Guid OrganizationId { get; init; }
}

public sealed class GetOrganizationCapabilitiesQueryHandler
    : IQueryHandler<GetOrganizationCapabilitiesQuery, OrganizationCapabilitiesResponse>
{
    private readonly CommunityProgrammeAccessService _access;

    public GetOrganizationCapabilitiesQueryHandler(CommunityProgrammeAccessService access) =>
        _access = access;

    public async Task<Result<OrganizationCapabilitiesResponse>> Handle(
        GetOrganizationCapabilitiesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var organization = await _access.FindOrganizationAsync(
            query.OrganizationId, cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationCapabilitiesResponse>(
                CommunityProgrammeErrors.OrganizationNotFound);
        }

        var capabilities = await _access.CapabilitiesAsync(query.OrganizationId, cancellationToken);

        return Result.Success(CommunityProgrammeMapper.ToResponse(capabilities));
    }
}

/// <summary>
/// The organization dashboard's first paint.
/// <para>
/// <b>One call, and the counts are the point.</b> They let a section be skipped rather than fetched
/// to discover it is empty — on an organization that only runs events, the difference between one
/// request and nine.
/// </para>
/// <para>
/// <b>A count the caller may not act on is not returned as zero; it is returned honestly.</b> A
/// Moderator sees the open-poll count because they may manage polls, and sees the pending-member
/// count as zero because they may not manage members — not because there are none. That distinction
/// is why each count is guarded individually rather than the whole object being refused: a member
/// with no management capability still gets the overview the register describes.
/// </para>
/// </summary>
[RequirePermission(CommunityProgrammePermissions.Read)]
public sealed record GetOrganizationDashboardQuery : IQuery<OrganizationDashboardResponse>
{
    public required Guid OrganizationId { get; init; }
}

public sealed class GetOrganizationDashboardQueryHandler
    : IQueryHandler<GetOrganizationDashboardQuery, OrganizationDashboardResponse>
{
    private readonly CommunityProgrammeAccessService _access;
    private readonly IProgrammeMembershipRepository _memberships;
    private readonly IVolunteerOpportunityRepository _opportunities;
    private readonly IVolunteerApplicationRepository _applications;
    private readonly ICommunityPollRepository _polls;
    private readonly ICommunityAnnouncementRepository _announcements;
    private readonly IOptionsMonitor<CommunityProgrammeOptions> _options;
    private readonly IClock _clock;

    public GetOrganizationDashboardQueryHandler(
        CommunityProgrammeAccessService access,
        IProgrammeMembershipRepository memberships,
        IVolunteerOpportunityRepository opportunities,
        IVolunteerApplicationRepository applications,
        ICommunityPollRepository polls,
        ICommunityAnnouncementRepository announcements,
        IOptionsMonitor<CommunityProgrammeOptions> options,
        IClock clock)
    {
        _access = access;
        _memberships = memberships;
        _opportunities = opportunities;
        _applications = applications;
        _polls = polls;
        _announcements = announcements;
        _options = options;
        _clock = clock;
    }

    public async Task<Result<OrganizationDashboardResponse>> Handle(
        GetOrganizationDashboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _access.RequireCaller();

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationDashboardResponse>(caller.Error);
        }

        var organization = await _access.FindOrganizationAsync(
            query.OrganizationId, cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationDashboardResponse>(
                CommunityProgrammeErrors.OrganizationNotFound);
        }

        var capabilities = await _access.CapabilitiesAsync(query.OrganizationId, cancellationToken);

        // Sequential, not concurrent, and that is not a missed optimisation. Every one of these runs
        // on the request's single scoped DbContext, and EF Core refuses a second operation on a
        // context while the first is in flight — the defect Wave 4 found in both Location aggregate
        // endpoints, where an aggregator turned the throw into "this vertical could not be asked" and
        // a hub rendered one number out of six with nothing reporting a fault. The SQLite provider
        // implements the async methods synchronously, so a test suite cannot see it; Npgsql is
        // genuinely asynchronous, and production would.
        var byStatus = capabilities.CanManageMembers
            ? await _memberships.CountByStatusAsync(query.OrganizationId, cancellationToken)
            : new Dictionary<ProgrammeMembershipStatus, int>();

        var openOpportunities = capabilities.CanManageVolunteers
            ? await _opportunities.CountByStatusAsync(
                query.OrganizationId, VolunteerOpportunityStatus.Published, cancellationToken)
            : 0;

        var pendingVolunteers = capabilities.CanManageVolunteers
            ? await _applications.CountPendingAsync(query.OrganizationId, cancellationToken)
            : 0;

        var openPolls = capabilities.CanManagePolls
            ? await _polls.CountOpenAsync(query.OrganizationId, cancellationToken)
            : 0;

        var since = _clock.UtcNow.AddDays(-_options.CurrentValue.RecentAnnouncementDays);

        var recentAnnouncements = capabilities.CanPublishAnnouncements
            ? await _announcements.CountSinceAsync(query.OrganizationId, since, cancellationToken)
            : 0;

        int Count(ProgrammeMembershipStatus status) =>
            byStatus.TryGetValue(status, out var value) ? value : 0;

        return Result.Success(new OrganizationDashboardResponse
        {
            OrganizationId = query.OrganizationId,
            OrganizationName = organization.Name,
            LogoMediaId = organization.LogoMediaId,
            Capabilities = CommunityProgrammeMapper.ToResponse(capabilities),
            Counts = new DashboardCountsResponse
            {
                PendingMemberApplications = Count(ProgrammeMembershipStatus.Pending),

                // Active and RenewalDue both, because a member inside their grace period has not
                // stopped being a member — the same rule that decides whether they keep their
                // benefits, stated once in the transition table and read here.
                ActiveMembers = Count(ProgrammeMembershipStatus.Active)
                    + Count(ProgrammeMembershipStatus.RenewalDue),
                OpenOpportunities = openOpportunities,
                PendingVolunteerApplications = pendingVolunteers,
                OpenPolls = openPolls,
                RecentAnnouncements = recentAnnouncements
            },
            ComputedAt = _clock.UtcNow
        });
    }
}
