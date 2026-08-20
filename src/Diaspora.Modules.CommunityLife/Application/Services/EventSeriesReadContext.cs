using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Rsvps;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Platform.Services.Abstractions;

namespace Diaspora.Modules.CommunityLife.Application.Services;

/// <summary>
/// Assembles everything a series response needs, once, for whichever handler asked.
/// <para>
/// The same job <c>CommunityEventReadContext</c> does for an event, and split from it rather than
/// folded into it because the questions differ: a series needs its next date and its category, and
/// has no attendee of its own to ask about.
/// </para>
/// <para>
/// <b>A page of series resolves its hosts and its next dates in two reads, not two per row.</b>
/// Twelve series sharing three host bodies asks for three organizations; twelve next-dates is
/// twelve rows from one query keyed on the series identifiers, not twelve queries.
/// </para>
/// </summary>
public sealed class EventSeriesReadContext
{
    private readonly IOrganizationRepository _organizations;
    private readonly ICommunityEventRepository _events;
    private readonly IEventRsvpRepository _rsvps;
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly IClock _clock;

    public EventSeriesReadContext(
        IOrganizationRepository organizations,
        ICommunityEventRepository events,
        IEventRsvpRepository rsvps,
        CommunityLifeCatalogResolver catalog,
        CommunityLifeAuthorizationService authorization,
        IClock clock)
    {
        _organizations = organizations;
        _events = events;
        _rsvps = rsvps;
        _catalog = catalog;
        _authorization = authorization;
        _clock = clock;
    }

    /// <summary>The full response for one series, with its next date.</summary>
    public async Task<EventSeriesResponse> ToResponseAsync(
        EventSeries series, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        var category = await _catalog.RequireCategoryAsync(
            series.CategoryKey, allowRetired: true, cancellationToken);

        var organization = series.OrganizationId is { } organizationId
            ? await _organizations.GetAsync(organizationId, cancellationToken)
            : null;

        var next = await _events.FindNextOccurrenceAsync(
            series.Id, _clock.UtcNow, cancellationToken);

        var isOrganizer = _authorization.CallerId is { } caller
            && (series.OrganizerUserId == caller || _authorization.IsStaff);

        var nextOccurrence = next is null
            ? null
            : EventSeriesMapper.ToOccurrence(
                next,
                series.Schedule,
                isOrganizer,
                await ViewerStatusAsync(next.Id, cancellationToken),
                isNext: true);

        return EventSeriesMapper.ToResponse(
            series,
            category.IsSuccess ? category.Value : null,
            organization?.Name.Value,
            organization?.Slug.Value,
            organization?.TrustMark.IsVerified ?? false,
            nextOccurrence);
    }

    /// <summary>A page of series. Hosts and next dates are batched; see the class summary.</summary>
    public async Task<IReadOnlyList<EventSeriesResponse>> ToResponsesAsync(
        IReadOnlyList<EventSeries> pages, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pages);

        if (pages.Count == 0)
        {
            return [];
        }

        var organizationIds = pages
            .Select(series => series.OrganizationId)
            .OfType<OrganizationId>()
            .Distinct()
            .ToList();

        var organizations = organizationIds.Count == 0
            ? []
            : (await _organizations.FindManyAsync(organizationIds, cancellationToken))
                .ToDictionary(organization => organization.Id);

        // Three reads for the whole page, not three per row: the hosts, the catalogue, and the next
        // date for every series at once. Asking per card is the N+1 the event read context already
        // avoids, and a page of twenty series would otherwise be sixty round trips.
        var categories = await _catalog.CategoryLookupAsync(cancellationToken);

        var nextOccurrences = await _events.FindNextOccurrencesAsync(
            [.. pages.Select(series => series.Id)], _clock.UtcNow, cancellationToken);

        return
        [
            .. pages.Select(series =>
            {
                var host = series.OrganizationId is { } organizationId
                    ? organizations.GetValueOrDefault(organizationId)
                    : null;

                var next = nextOccurrences.GetValueOrDefault(series.Id);

                return EventSeriesMapper.ToResponse(
                    series,
                    categories.GetValueOrDefault(series.CategoryKey),
                    host?.Name.Value,
                    host?.Slug.Value,
                    host?.TrustMark.IsVerified ?? false,
                    next is null
                        ? null
                        : EventSeriesMapper.ToOccurrence(
                            next, series.Schedule, viewerIsOrganizer: false, isNext: true));
            })
        ];
    }

    /// <summary>
    /// A page of one series' dates, with the caller's own response against each.
    /// </summary>
    /// <param name="nextOccurrenceId">
    /// The soonest date the series still has, so exactly one row on the page can be marked as next.
    /// Resolved by the caller because a windowed page may not contain it at all.
    /// </param>
    public async Task<IReadOnlyList<EventOccurrenceResponse>> ToOccurrencesAsync(
        EventSeries series,
        IReadOnlyList<CommunityEvent> occurrences,
        CommunityEventId? nextOccurrenceId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(occurrences);

        if (occurrences.Count == 0)
        {
            return [];
        }

        var isOrganizer = _authorization.CallerId is { } caller
            && (series.OrganizerUserId == caller || _authorization.IsStaff);

        var viewerStatuses = await ViewerStatusesAsync(occurrences, cancellationToken);

        return
        [
            .. occurrences.Select(occurrence => EventSeriesMapper.ToOccurrence(
                occurrence,
                series.Schedule,
                isOrganizer,
                viewerStatuses.GetValueOrDefault(occurrence.Id),
                nextOccurrenceId is not null && occurrence.Id == nextOccurrenceId))
        ];
    }

    private async Task<string?> ViewerStatusAsync(
        CommunityEventId eventId, CancellationToken cancellationToken)
    {
        if (_authorization.CallerId is not { } callerId)
        {
            return null;
        }

        var rsvp = await _rsvps.FindAsync(eventId, callerId, cancellationToken);

        return rsvp?.Status.ToString();
    }

    private async Task<IReadOnlyDictionary<CommunityEventId, string>> ViewerStatusesAsync(
        IReadOnlyList<CommunityEvent> occurrences, CancellationToken cancellationToken)
    {
        if (_authorization.CallerId is not { } callerId)
        {
            return new Dictionary<CommunityEventId, string>();
        }

        var responses = await _rsvps.FindForAttendeeAsync(
            callerId,
            [.. occurrences.Select(occurrence => occurrence.Id)],
            cancellationToken);

        return responses.ToDictionary(rsvp => rsvp.EventId, rsvp => rsvp.Status.ToString());
    }
}
