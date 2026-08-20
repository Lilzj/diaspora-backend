using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Assembles everything an event response needs, once, for whichever handler asked.
/// <para>
/// <b>Rendering one event means answering five questions that live in four places:</b> what its
/// category is called and which facets it carries, who is hosting it and whether that body is
/// verified, whether the caller is running it, and what the caller has already said about coming.
/// Every handler that returns an event needs all five. The one that matters if it is wrong is the
/// last: an event page that forgot the caller's response would invite somebody to respond twice.
/// </para>
/// <para>
/// <b>Card rendering resolves its hosts in one batched read.</b> A page of twenty events sharing
/// four host bodies asks for four organizations, not twenty — and it asks the viewer question once
/// per page rather than per row, because the caller's own responses come back in a single query.
/// </para>
/// </summary>
public sealed class CommunityEventReadContext
{
    private readonly IOrganizationRepository _organizations;
    private readonly IEventRsvpRepository _rsvps;
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IClock _clock;

    public CommunityEventReadContext(
        IOrganizationRepository organizations,
        IEventRsvpRepository rsvps,
        CommunityLifeCatalogResolver catalog,
        CommunityLifeAuthorizationService authorization,
        IClock clock)
    {
        _organizations = organizations;
        _rsvps = rsvps;
        _catalog = catalog;
        _authorization = authorization;
        _clock = clock;
    }

    /// <summary>The full detail response for one event.</summary>
    public async Task<EventResponse> ToResponseAsync(
        CommunityEvent communityEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communityEvent);

        var category = await _catalog.RequireCategoryAsync(
            communityEvent.CategoryKey, allowRetired: true, cancellationToken);

        var organization = communityEvent.OrganizationId is { } organizationId
            ? await _organizations.GetAsync(organizationId, cancellationToken)
            : null;

        var viewerRsvp = _authorization.CallerId is { } callerId
            ? await _rsvps.FindAsync(communityEvent.Id, callerId, cancellationToken)
            : null;

        var isOrganizer = _authorization.CallerId is { } caller
            && (communityEvent.OrganizerUserId == caller || _authorization.IsStaff);

        return CommunityEventMapper.ToResponse(
            communityEvent,
            category.IsSuccess ? category.Value : null,
            organization?.Name.Value,
            organization?.Slug.Value,
            organization?.TrustMark.IsVerified ?? false,
            viewerRsvp,
            isOrganizer,
            _clock.UtcNow);
    }

    /// <summary>
    /// Cards for a page of events.
    /// <para>
    /// The distances are supplied by the caller rather than computed here: only a nearby search
    /// knows where the member was standing, and every other page would pay for a column it does not
    /// render.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<EventSummaryResponse>> ToSummariesAsync(
        IReadOnlyList<CommunityEvent> events,
        IReadOnlyDictionary<CommunityEventId, double>? distancesKm = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(events);

        if (events.Count == 0)
        {
            return [];
        }

        var organizations = await HostsAsync(events, cancellationToken);
        var viewerStatuses = await ViewerStatusesAsync(events, cancellationToken);

        return
        [
            .. events.Select(communityEvent =>
            {
                var host = communityEvent.OrganizationId is { } organizationId
                    ? organizations.GetValueOrDefault(organizationId)
                    : null;

                return CommunityEventMapper.ToSummary(
                    communityEvent,
                    host?.Name.Value,
                    host?.TrustMark.IsVerified ?? false,
                    distancesKm?.GetValueOrDefault(communityEvent.Id),
                    viewerStatuses.GetValueOrDefault(communityEvent.Id));
            })
        ];
    }

    /// <summary>The host bodies for a page, in one read. Events with no host cost nothing.</summary>
    private async Task<IReadOnlyDictionary<OrganizationId, Organization>> HostsAsync(
        IReadOnlyList<CommunityEvent> events, CancellationToken cancellationToken)
    {
        var ids = events
            .Select(communityEvent => communityEvent.OrganizationId)
            .OfType<OrganizationId>()
            .Distinct()
            .ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<OrganizationId, Organization>();
        }

        var organizations = await _organizations.FindManyAsync(ids, cancellationToken);

        return organizations.ToDictionary(organization => organization.Id);
    }

    /// <summary>
    /// What the caller has already said about each event on the page.
    /// <para>
    /// Read through the RSVP filter in one query rather than one per card. An anonymous page skips
    /// it entirely — there is nobody to have responded.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyDictionary<CommunityEventId, string>> ViewerStatusesAsync(
        IReadOnlyList<CommunityEvent> events, CancellationToken cancellationToken)
    {
        if (_authorization.CallerId is not { } callerId)
        {
            return new Dictionary<CommunityEventId, string>();
        }

        var responses = await _rsvps.FindForAttendeeAsync(
            callerId,
            [.. events.Select(communityEvent => communityEvent.Id)],
            cancellationToken);

        return responses.ToDictionary(
            rsvp => rsvp.EventId, rsvp => rsvp.Status.ToString());
    }
}
