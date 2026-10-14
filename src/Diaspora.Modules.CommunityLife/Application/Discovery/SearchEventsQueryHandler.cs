using Diaspora.Application.Abstractions;
using Diaspora.Modules.CommunityLife.Application.Mapping;
using Diaspora.Modules.CommunityLife.Application.Services;
using Diaspora.Modules.CommunityLife.Contracts.V1.Responses;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Extensibility.Search;
using Diaspora.Modules.CommunityLife.Observability;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.CommunityLife.Application.Discovery;

/// <summary>
/// The event browse screen.
/// <para>
/// <b>The search engine is a seam, and the aggregates are always hydrated from the database.</b> An
/// engine returns identifiers and an ordering; the rows come from here. That is what stops a stale
/// index from serving an event that has since been cancelled, hidden or deleted — the worst a stale
/// index can do is omit something or return an identifier that no longer resolves.
/// </para>
/// <para>
/// <b>A facet resolves to category keys before the query runs.</b> One indexed predicate over a
/// column beats a join into the catalogue on every row, and the catalogue is small enough that
/// resolving it once per search costs nothing.
/// </para>
/// <para>
/// <b>The caller's organizations are resolved once and passed into the filter.</b> That is what
/// makes a members-only event visible to the right people without a membership join on every row.
/// </para>
/// </summary>
public sealed class SearchEventsQueryHandler
    : IQueryHandler<SearchEventsQuery, EventSearchResultResponse>
{
    private readonly ICommunityEventRepository _events;
    private readonly ICommunityEventSearchEngine _engine;
    private readonly CommunityLifeCatalogResolver _catalog;
    private readonly CommunityLifeNeighborhoodResolver _neighborhoods;
    private readonly CommunityLifeAuthorizationService _authorization;
    private readonly CommunityLifeGeographicScopeAdapter _geography;
    private readonly CommunityEventReadContext _read;

    public SearchEventsQueryHandler(
        ICommunityEventRepository events,
        ICommunityEventSearchEngine engine,
        CommunityLifeCatalogResolver catalog,
        CommunityLifeNeighborhoodResolver neighborhoods,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeGeographicScopeAdapter geography,
        CommunityEventReadContext read)
    {
        _geography = geography;
        _events = events;
        _engine = engine;
        _catalog = catalog;
        _neighborhoods = neighborhoods;
        _authorization = authorization;
        _read = read;
    }

    public async Task<Result<EventSearchResultResponse>> Handle(
        SearchEventsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var filter = new EventQueryFilter
        {
            SearchTerm = query.SearchTerm,
            CategoryKeys = await ResolveCategoriesAsync(query, cancellationToken),
            OrganizationId = query.OrganizationId is { } organizationId
                && organizationId != Guid.Empty
                ? OrganizationId.From(organizationId)
                : null,
            CountryCode = query.CountryCode,
            CityName = query.CityName,
            NeighborhoodAreaId = query.NeighborhoodAreaId,
            AreaPathPrefix = await _neighborhoods.AreaPathAsync(
                query.NeighborhoodAreaId, cancellationToken),
            StartsAfter = query.StartsAfter,
            StartsBefore = query.StartsBefore,
            FreeOnly = query.FreeOnly,
            FamilyFriendlyOnly = query.FamilyFriendlyOnly,
            ChildFriendlyOnly = query.ChildFriendlyOnly,
            StudentOnly = query.StudentOnly,
            FeaturedOnly = query.FeaturedOnly,
            VerifiedOrganizersOnly = query.VerifiedOrganizersOnly,
            HasPlacesRemaining = query.HasPlacesRemaining,
            AttendanceMode = CommunityLifeSharedMapper.ParseEnum<EventAttendanceMode>(
                query.AttendanceMode),
            Statuses = [EventStatus.Published, EventStatus.InProgress],
            ViewerUserId = _authorization.CallerId,
            ViewerOrganizationIds = await _authorization.CallerOrganizationsAsync(
                cancellationToken),

            // A browse screen is a public surface. Drafts and archived events belong to the
            // organizer's own list.
            IncludeNonPublic = false,
            IncludePast = query.IncludePast,
            SortOrder = CommunityLifeSharedMapper.ParseEnum<EventSortOrder>(query.SortOrder)
                ?? EventSortOrder.Soonest,
            Cursor = query.Cursor,
            Limit = Math.Clamp(query.Limit ?? 20, 1, CommunityLifeLimits.MaxPageSize)
        };

        // A viewport supersedes the city and neighbourhood narrowing rather than adding to it: the
        // member has moved the map, and intersecting the rectangle with the name they typed before
        // they moved it answers neither question.
        //
        // Since 14 Oct 2026 a point and a typed town are narrowed here too, without the place
        // catalogue: a circle by coordinates, a town by its name in either letter case.
        if (!string.IsNullOrWhiteSpace(query.Bounds)
            || (query.Latitude is not null && query.Longitude is not null)
            || !string.IsNullOrWhiteSpace(query.CityName))
        {
            var narrowed = await _geography.NarrowAsync(
                filter,
                query.CityName,
                query.CountryCode,
                query.Latitude,
                query.Longitude,
                query.RadiusKm,
                query.Bounds,
                _authorization.CallerId,
                cancellationToken);

            if (narrowed.IsFailure)
            {
                return Result.Failure<EventSearchResultResponse>(narrowed.Error);
            }

            filter = narrowed.Value;
        }

        var result = await _engine.SearchAsync(filter, cancellationToken);

        var events = await HydrateAsync(result, cancellationToken);

        CommunityLifeTelemetry.SearchesRun.Add(
            1,
            new KeyValuePair<string, object?>("subject", "event"),
            new KeyValuePair<string, object?>("engine", _engine.EngineName));

        return Result.Success(new EventSearchResultResponse
        {
            Items = await _read.ToSummariesAsync(events, cancellationToken: cancellationToken),
            NextCursor = result.NextCursor,
            HasMore = result.HasMore,
            TotalCount = result.TotalCount,
            Facets = result.Facets,
            Engine = _engine.EngineName
        });
    }

    /// <summary>
    /// The category keys to filter on: whatever the caller named, plus whatever the facet expands to.
    /// <para>
    /// A facet that expands to nothing is left out rather than producing an empty filter, so a stale
    /// facet name in a bookmarked link widens the search instead of emptying it.
    /// </para>
    /// </summary>
    private async Task<IReadOnlyList<string>?> ResolveCategoriesAsync(
        SearchEventsQuery query, CancellationToken cancellationToken)
    {
        var keys = new List<string>();

        if (query.CategoryKeys is { Count: > 0 })
        {
            keys.AddRange(query.CategoryKeys);
        }

        if (!string.IsNullOrWhiteSpace(query.Facet))
        {
            keys.AddRange(await _catalog.KeysForFacetAsync(query.Facet, cancellationToken));
        }

        return keys.Count == 0 ? null : [.. keys.Distinct(StringComparer.Ordinal)];
    }

    /// <summary>Loads the events the engine named, keeping its ordering.</summary>
    private async Task<IReadOnlyList<CommunityEvent>> HydrateAsync(
        EventSearchResult result, CancellationToken cancellationToken)
    {
        if (result.Hits.Count == 0)
        {
            return [];
        }

        var ids = result.Hits.Select(hit => CommunityEventId.From(hit.EventId)).ToList();

        var loaded = await _events.FindManyAsync(ids, cancellationToken);
        var byId = loaded.ToDictionary(communityEvent => communityEvent.Id);

        return [.. ids.Select(byId.GetValueOrDefault).OfType<CommunityEvent>()];
    }
}
