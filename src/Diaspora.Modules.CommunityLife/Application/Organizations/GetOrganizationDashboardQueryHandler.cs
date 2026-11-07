using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.Analytics;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Errors;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Memberships;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Organizations;

/// <summary>
/// What an administrator sees when they open their organization.
/// <para>
/// <b>The pending requests lead, because that is the number most likely to be acted on within the
/// hour.</b> Somebody waiting to be let into a support group or a parents' association is waiting on
/// a person, and a dashboard that buried the queue below a chart would leave them waiting longer.
/// </para>
/// <para>
/// <b>Assembled in four reads and no loops.</b> A dashboard that resolved its events one at a time
/// would be the slowest screen in the module and the one administrators open most.
/// </para>
/// </summary>
public sealed class GetOrganizationDashboardQueryHandler
    : IQueryHandler<GetOrganizationDashboardQuery, OrganizationDashboardResponse>
{
    private const int PendingLimit = 20;
    private const int EventLimit = 10;

    private readonly IOrganizationRepository _organizations;
    private readonly IOrganizationMembershipRepository _memberships;
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationAnalyticsRepository _analytics;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityEventReadContext _read;
    private readonly IClock _clock;

    public GetOrganizationDashboardQueryHandler(
        IOrganizationRepository organizations,
        IOrganizationMembershipRepository memberships,
        ICommunityEventRepository events,
        IOrganizationAnalyticsRepository analytics,
        CommunityLifeAuthorizationService authorization,
        CommunityEventReadContext read,
        IClock clock)
    {
        _organizations = organizations;
        _memberships = memberships;
        _events = events;
        _analytics = analytics;
        _authorization = authorization;
        _read = read;
        _clock = clock;
    }

    public async Task<Result<OrganizationDashboardResponse>> Handle(
        GetOrganizationDashboardQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var organizationId = OrganizationId.From(query.OrganizationId);

        var organization = await _organizations.GetAsync(organizationId, cancellationToken);

        if (organization is null)
        {
            return Result.Failure<OrganizationDashboardResponse>(
                CommunityLifeErrors.OrganizationNotFound);
        }

        var caller = await _authorization.RequireOrganizationManagementReadAsync(
            organization, cancellationToken);

        if (caller.IsFailure)
        {
            return Result.Failure<OrganizationDashboardResponse>(caller.Error);
        }

        var pending = await _memberships.SearchAsync(
            new MembershipQueryFilter
            {
                OrganizationId = organizationId,
                Statuses = [MembershipStatus.Requested, MembershipStatus.Invited],
                Limit = PendingLimit
            },
            cancellationToken);

        var upcoming = await EventsAsync(organizationId, upcoming: true, cancellationToken);
        var recent = await EventsAsync(organizationId, upcoming: false, cancellationToken);

        var analytics = await _analytics.FindByOrganizationAsync(
            organizationId, cancellationToken);

        return Result.Success(new OrganizationDashboardResponse
        {
            Organization = OrganizationMapper.ToSummary(organization),
            Statistics = OrganizationMapper.ToResponse(organization, viewerIsAdministrator: true),
            Analytics = CommunityLifeCatalogueMapper.ToResponse(
                analytics ?? OpenTransient(organization)),
            PendingRequests = [.. pending.Items.Select(OrganizationMapper.ToResponse)],
            UpcomingEvents = await _read.ToSummariesAsync(
                upcoming, cancellationToken: cancellationToken),
            RecentEvents = await _read.ToSummariesAsync(
                recent, cancellationToken: cancellationToken),
            Completeness = organization.Completeness
        });
    }

    /// <summary>
    /// The body's next events, or its last ones.
    /// <para>
    /// Both pages include drafts, because this is the one screen where an organizer needs to see
    /// what they have not published yet.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<CommunityEvent>> EventsAsync(
        OrganizationId organizationId, bool upcoming, CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var page = await _events.SearchAsync(
            new EventQueryFilter
            {
                OrganizationId = organizationId,
                StartsAfter = upcoming ? now : null,
                StartsBefore = upcoming ? null : now,
                IncludeNonPublic = true,
                IncludePast = !upcoming,
                ViewerOrganizationIds = [organizationId],
                SortOrder = upcoming ? EventSortOrder.Soonest : EventSortOrder.Newest,
                Limit = EventLimit
            },
            cancellationToken);

        return page.Items;
    }

    /// <summary>
    /// A zeroed analytics record for a body whose row has not been opened.
    /// <para>
    /// Built rather than persisted, and never added to the repository: this covers organizations
    /// that predate the record being opened at creation, and a dashboard rendering zeros is better
    /// than one that fails. Writing it here would make a read silently create data.
    /// </para>
    /// </summary>
    private static OrganizationAnalytics OpenTransient(Organization organization) =>
        OrganizationAnalytics.Open(
            OrganizationAnalyticsId.From(organization.Id.Value),
            organization.Id,
            organization.Location.CountryCode,
            organization.OrganizationTypeKey,
            organization.Location.NeighborhoodAreaId,
            organization.CreatedAt);
}
