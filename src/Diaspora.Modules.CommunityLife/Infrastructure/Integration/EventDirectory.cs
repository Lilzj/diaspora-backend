using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Integration;

namespace Diaspora.Modules.CommunityLife.Infrastructure.Integration;

/// <summary>
/// What other modules may ask B12 about an event.
/// <para>
/// <b>Public events only.</b> A consumer has no viewer and therefore no audience, so a members-only
/// or unlisted event is invisible here — a module rendering "events near you" must not be the route
/// by which a private gathering reaches somebody the organizer excluded.
/// </para>
/// <para>
/// <b>The host bodies are resolved in one batched read.</b> A consumer asking for ten upcoming
/// events gets ten names for two queries rather than eleven.
/// </para>
/// </summary>
public sealed class EventDirectory : IEventDirectory
{
    private readonly ICommunityEventRepository _events;
    private readonly IOrganizationRepository _organizations;

    public EventDirectory(
        ICommunityEventRepository events, IOrganizationRepository organizations)
    {
        _events = events;
        _organizations = organizations;
    }

    public async Task<EventSummary?> FindAsync(
        Guid eventId, CancellationToken cancellationToken = default)
    {
        if (eventId == Guid.Empty)
        {
            return null;
        }

        var communityEvent = await _events.GetAsync(
            CommunityEventId.From(eventId), cancellationToken);

        return communityEvent is null
            ? null
            : await DescribeAsync(communityEvent, cancellationToken);
    }

    public async Task<EventSummary?> FindBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var communityEvent = await _events.GetBySlugAsync(slug, cancellationToken);

        return communityEvent is null || communityEvent.IsDeleted
            ? null
            : await DescribeAsync(communityEvent, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, EventSummary>> FindManyAsync(
        IReadOnlyCollection<Guid> eventIds, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventIds);

        if (eventIds.Count == 0)
        {
            return new Dictionary<Guid, EventSummary>();
        }

        var events = await _events.FindManyAsync(
            [.. eventIds.Where(id => id != Guid.Empty).Distinct()
                .Select(CommunityEventId.From)],
            cancellationToken);

        var described = await DescribeManyAsync(events, cancellationToken);

        return described.ToDictionary(summary => summary.EventId);
    }

    public async Task<IReadOnlyList<EventSummary>> FindUpcomingAsync(
        string? countryCode = null,
        Guid? neighborhoodAreaId = null,
        Guid? organizationId = null,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var page = await _events.SearchAsync(
            new EventQueryFilter
            {
                CountryCode = countryCode,
                NeighborhoodAreaId = neighborhoodAreaId,
                OrganizationId = organizationId is { } id && id != Guid.Empty
                    ? OrganizationId.From(id)
                    : null,
                Statuses = [Domain.Enums.EventStatus.Published],

                // No viewer, so no members-only or unlisted event can reach a consumer. See the
                // class summary.
                IncludeNonPublic = false,
                IncludePast = false,
                SortOrder = Domain.Enums.EventSortOrder.Soonest,
                Limit = Math.Clamp(limit, 1, 50)
            },
            cancellationToken);

        return await DescribeManyAsync(page.Items, cancellationToken);
    }

    private async Task<EventSummary> DescribeAsync(
        CommunityEvent communityEvent, CancellationToken cancellationToken)
    {
        var organization = communityEvent.OrganizationId is { } organizationId
            ? await _organizations.GetAsync(organizationId, cancellationToken)
            : null;

        return CommunityEventMapper.ToPublished(
            communityEvent,
            organization?.Name.Value,
            organization?.TrustMark.IsVerified ?? false);
    }

    private async Task<IReadOnlyList<EventSummary>> DescribeManyAsync(
        IReadOnlyList<CommunityEvent> events, CancellationToken cancellationToken)
    {
        if (events.Count == 0)
        {
            return [];
        }

        var hostIds = events
            .Select(communityEvent => communityEvent.OrganizationId)
            .OfType<OrganizationId>()
            .Distinct()
            .ToList();

        var hosts = hostIds.Count == 0
            ? []
            : await _organizations.FindManyAsync(hostIds, cancellationToken);

        var byId = hosts.ToDictionary(organization => organization.Id);

        return
        [
            .. events.Select(communityEvent =>
            {
                var host = communityEvent.OrganizationId is { } organizationId
                    ? byId.GetValueOrDefault(organizationId)
                    : null;

                return CommunityEventMapper.ToPublished(
                    communityEvent,
                    host?.Name.Value,
                    host?.TrustMark.IsVerified ?? false);
            })
        ];
    }
}
