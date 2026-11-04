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
using Diaspora.Contracts.CommunityProgramme.V1.Directory;
using Diaspora.SharedKernel.Geography;
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
    private readonly IReadOnlyList<IMemberCommunityDirectory> _communities;

    public SearchEventsQueryHandler(
        ICommunityEventRepository events,
        ICommunityEventSearchEngine engine,
        CommunityLifeCatalogResolver catalog,
        CommunityLifeNeighborhoodResolver neighborhoods,
        CommunityLifeAuthorizationService authorization,
        CommunityLifeGeographicScopeAdapter geography,
        CommunityEventReadContext read,
        IEnumerable<IMemberCommunityDirectory> communities)
    {
        _communities = [.. communities];
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
            CommunityId = query.CommunityId is { } communityId && communityId != Guid.Empty
                ? communityId
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

        // "My communities" (4 Nov 2026): the communities the caller belongs to, asked of the module
        // that owns them. Nobody signed in, or a member of none, is an empty page, not every event.
        if (query.JoinedCommunitiesOnly)
        {
            var mine = _authorization.CallerId is { } caller
                ? await CommunitiesOfAsync(caller.Value, cancellationToken)
                : [];

            if (mine.Count == 0)
            {
                return Result.Success(Empty());
            }

            filter = filter with { CommunityIds = mine };
        }

        // Around a point with a radius in kilometres (UAT, 3 Nov 2026): the geography narrowed to
        // the circle's bounding box above; here each event is measured, the box's corners are
        // dropped, and Nearest sorts by the measurement.
        var circle = AroundPoint(query);

        if (circle is not null && filter.SortOrder == EventSortOrder.Nearest)
        {
            return Result.Success(await NearestAsync(filter, circle, cancellationToken));
        }

        var result = await _engine.SearchAsync(filter, cancellationToken);

        var events = await HydrateAsync(result, cancellationToken);
        var distances = circle is null ? null : Measure(events, circle);

        if (circle is not null)
        {
            events = [.. events.Where(communityEvent => Keeps(communityEvent, circle, distances!))];
        }

        CommunityLifeTelemetry.SearchesRun.Add(
            1,
            new KeyValuePair<string, object?>("subject", "event"),
            new KeyValuePair<string, object?>("engine", _engine.EngineName));

        return Result.Success(new EventSearchResultResponse
        {
            Items = await _read.ToSummariesAsync(events, distances, cancellationToken),
            NextCursor = result.NextCursor,
            HasMore = result.HasMore,
            TotalCount = result.TotalCount,
            Facets = result.Facets,
            Engine = _engine.EngineName
        });
    }

    /// <summary>The most events a Nearest page is chosen from: the soonest, five pages of them.</summary>
    private const int NearestCandidatePages = 5;

    /// <summary>
    /// Nearest first. The database has no distance to sort by (venues are two decimal columns, not
    /// a geography), so the soonest events in the box are read — at most five pages — measured,
    /// kept to the circle, sorted by distance and then time, and paged here by offset. An event
    /// with no venue coordinates, the same-town ones the box admits, sorts last.
    /// </summary>
    private async Task<EventSearchResultResponse> NearestAsync(
        EventQueryFilter filter, CircleArea circle, CancellationToken cancellationToken)
    {
        var candidates = new List<CommunityEvent>();
        var page = filter with { SortOrder = EventSortOrder.Soonest, Cursor = null, Limit = CommunityLifeLimits.MaxPageSize };

        for (var read = 0; read < NearestCandidatePages; read++)
        {
            var result = await _engine.SearchAsync(page, cancellationToken);
            candidates.AddRange(await HydrateAsync(result, cancellationToken));

            if (!result.HasMore || result.NextCursor is null)
            {
                break;
            }

            page = page with { Cursor = result.NextCursor };
        }

        var distances = Measure(candidates, circle);

        var ordered = candidates
            .Where(communityEvent => Keeps(communityEvent, circle, distances))
            .OrderBy(communityEvent => distances.TryGetValue(communityEvent.Id, out var km) ? km : double.MaxValue)
            .ThenBy(communityEvent => communityEvent.Schedule.StartsAt)
            .ToList();

        var offset = NearestOffset(filter.Cursor);
        var slice = ordered.Skip(offset).Take(filter.Limit).ToList();
        var hasMore = offset + slice.Count < ordered.Count;

        return new EventSearchResultResponse
        {
            Items = await _read.ToSummariesAsync(slice, distances, cancellationToken),
            NextCursor = hasMore ? $"nearest:{offset + slice.Count}" : null,
            HasMore = hasMore,
            TotalCount = null,
            Engine = _engine.EngineName
        };
    }

    private static int NearestOffset(string? cursor) =>
        cursor is not null
        && cursor.StartsWith("nearest:", StringComparison.Ordinal)
        && int.TryParse(cursor["nearest:".Length..], System.Globalization.NumberStyles.None,
            System.Globalization.CultureInfo.InvariantCulture, out var offset)
            ? offset
            : 0;

    /// <summary>The circle a point and a positive radius describe; null for a town or a country.</summary>
    private static CircleArea? AroundPoint(SearchEventsQuery query) =>
        query.Latitude is { } latitude
        && query.Longitude is { } longitude
        && query.RadiusKm is { } radius
        && radius > 0
        && latitude is >= -90 and <= 90
        && longitude is >= -180 and <= 180
        && string.IsNullOrWhiteSpace(query.Bounds)
            ? CircleArea.Around(latitude, longitude, radius)
            : null;

    private static Dictionary<CommunityEventId, double> Measure(
        IEnumerable<CommunityEvent> events, CircleArea circle)
    {
        var distances = new Dictionary<CommunityEventId, double>();

        foreach (var communityEvent in events)
        {
            if (communityEvent.Venue?.Latitude is { } latitude && communityEvent.Venue.Longitude is { } longitude)
            {
                distances[communityEvent.Id] = Math.Round(
                    circle.DistanceKm(latitude, longitude), 2);
            }
        }

        return distances;
    }

    /// <summary>Inside the circle, or without coordinates (a same-town event the box admitted).</summary>
    private static bool Keeps(
        CommunityEvent communityEvent, CircleArea circle, IReadOnlyDictionary<CommunityEventId, double> distances) =>
        !distances.TryGetValue(communityEvent.Id, out var km) || km <= circle.RadiusKm;

    private async Task<IReadOnlyCollection<Guid>> CommunitiesOfAsync(Guid memberId, CancellationToken cancellationToken)
    {
        var ids = new HashSet<Guid>();

        foreach (var directory in _communities)
        {
            ids.UnionWith(await directory.CommunitiesOfAsync(memberId, cancellationToken));
        }

        return ids;
    }

    private EventSearchResultResponse Empty() => new()
    {
        Items = [],
        NextCursor = null,
        HasMore = false,
        TotalCount = 0,
        Engine = _engine.EngineName
    };

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
