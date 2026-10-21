using System.Diagnostics.CodeAnalysis;
using Diaspora.Modules.CommunityLife.Domain.CommunityEvents;
using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>
/// EF Core repository for events.
/// <para>
/// <b>The default page is ordered soonest-first and that is the module's one departure from every
/// other catalogue on the platform.</b> A housing listing, a job and a marketplace item do not
/// expire, so newest-first is right for all three; an event does, and a member looking at a list
/// wants the one they can still get to.
/// </para>
/// <para>
/// <b>Past events are excluded by default and that is a filter, not an ordering.</b>
/// <c>IncludePast</c> exists because an organizer's own dashboard and an analytics read both want
/// them, and a discovery feed emphatically does not.
/// </para>
/// </summary>
public sealed class CommunityEventRepository : ICommunityEventRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CommunityEventRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CommunityEvent> Events => _dbContext.Set<CommunityEvent>();

    public async Task<CommunityEvent?> GetAsync(
        CommunityEventId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Events.FirstOrDefaultAsync(
            communityEvent => communityEvent.Id == id, cancellationToken);
    }

    public async Task<CommunityEvent?> GetIncludingDeletedAsync(
        CommunityEventId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Events
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(communityEvent => communityEvent.Id == id, cancellationToken);
    }

    public async Task<CommunityEvent?> GetBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var normalized = CommunityLifeSlug.Slugify(slug);

        if (normalized is null)
        {
            return null;
        }

        // IgnoreQueryFilters: a link to a funeral or a wedding is pinned in messages years old and
        // should resolve to "this has gone" rather than to something that looks like a broken app.
        return await Events
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                communityEvent => communityEvent.Slug.Value == normalized, cancellationToken);
    }

    public async Task<CursorPagedResult<CommunityEvent>> SearchAsync(
        EventQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Events.AsNoTracking(), filter);
        var parsed = CommunityLifeCursor.Parse(filter.Cursor);

        query = ApplyOrderAndCursor(query, filter, parsed);

        var limit = Math.Clamp(filter.Limit, 1, CommunityLifeLimits.MaxPageSize);
        var rows = await query.Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0 ? NextCursor(rows[^1], filter.SortOrder) : null;

        return CursorPagedResult<CommunityEvent>.Create(rows, next, hasMore);
    }

    public async Task<IReadOnlyList<CommunityEvent>> FindManyAsync(
        IReadOnlyCollection<CommunityEventId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        var wanted = ids.Distinct().ToList();

        return await Events
            .AsNoTracking()
            .Where(communityEvent => wanted.Contains(communityEvent.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasDuplicateAsync(
        UserId organizerUserId,
        string? title,
        DateTimeOffset startsAt,
        CommunityEventId? excluding,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizerUserId);

        var normalized = CommunityLifeTitle.Normalize(title);

        if (normalized is null)
        {
            return false;
        }

        // Same organizer, same title, same day. Not the same instant: four committee members
        // posting one festival will each pick a slightly different start time, and an exact-instant
        // check would catch none of them.
        var dayStart = new DateTimeOffset(startsAt.UtcDateTime.Date, TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        return await Events
            .AsNoTracking()
            .AnyAsync(
                communityEvent => communityEvent.OrganizerUserId == organizerUserId
                    && communityEvent.Title.Value == normalized
                    && communityEvent.Schedule.StartsAt >= dayStart
                    && communityEvent.Schedule.StartsAt < dayEnd
                    && (excluding == null || communityEvent.Id != excluding),
                cancellationToken);
    }

    public async Task<int> CountLiveByOrganizerAsync(
        UserId organizerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizerUserId);

        return await Events
            .AsNoTracking()
            .CountAsync(
                communityEvent => communityEvent.OrganizerUserId == organizerUserId
                    && (communityEvent.Status == EventStatus.Draft
                        || communityEvent.Status == EventStatus.Published
                        || communityEvent.Status == EventStatus.InProgress),
                cancellationToken);
    }

    public async Task<bool> SlugExistsAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var normalized = CommunityLifeSlug.Slugify(slug);

        if (normalized is null)
        {
            return false;
        }

        return await Events
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(
                communityEvent => communityEvent.Slug.Value == normalized, cancellationToken);
    }

    public async Task<int> CountInAreaAsync(
        string areaPathPrefix, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(areaPathPrefix))
        {
            return 0;
        }

        return await Events
            .AsNoTracking()
            .CountAsync(
                communityEvent => communityEvent.Location.AreaPath != null
                    && communityEvent.Location.AreaPath.StartsWith(areaPathPrefix)
                    && communityEvent.Status == EventStatus.Published
                    && communityEvent.Visibility == EventVisibility.Public
                    && communityEvent.Schedule.EndsAt >= asOf,
                cancellationToken);
    }

    public async Task<int> CountInCountryAsync(
        string countryCode, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        var normalized = countryCode?.Trim().ToUpperInvariant();

        if (string.IsNullOrEmpty(normalized))
        {
            return 0;
        }

        return await Events
            .AsNoTracking()
            .CountAsync(
                communityEvent => communityEvent.Location.CountryCode == normalized
                    && communityEvent.Status == EventStatus.Published
                    && communityEvent.Visibility == EventVisibility.Public
                    && communityEvent.Schedule.EndsAt >= asOf,
                cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityEvent>> FindStartedAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await Events
            .Where(communityEvent => communityEvent.Status == EventStatus.Published
                && communityEvent.Schedule.StartsAt <= asOf)
            .OrderBy(communityEvent => communityEvent.Schedule.StartsAt)
            .Take(Math.Clamp(batchSize, 1, 1000))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CommunityEvent>> FindEndedAsync(
        DateTimeOffset asOf, int batchSize, CancellationToken cancellationToken = default) =>
        await Events
            .Where(communityEvent => (communityEvent.Status == EventStatus.InProgress
                    || communityEvent.Status == EventStatus.Published)
                && communityEvent.Schedule.EndsAt <= asOf)
            .OrderBy(communityEvent => communityEvent.Schedule.EndsAt)
            .Take(Math.Clamp(batchSize, 1, 1000))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CommunityEvent>> FindInWindowAsync(
        DateTimeOffset from,
        DateTimeOffset to,
        string? countryCode,
        Guid? neighborhoodAreaId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Events
            .AsNoTracking()
            .Where(communityEvent =>
                (communityEvent.Status == EventStatus.Published
                    || communityEvent.Status == EventStatus.InProgress)
                && communityEvent.Visibility == EventVisibility.Public
                && communityEvent.ModerationState != CommunityLifeModerationState.Hidden
                && communityEvent.ModerationState != CommunityLifeModerationState.Suspended
                && communityEvent.Schedule.StartsAt < to
                && communityEvent.Schedule.EndsAt > from);

        if (!string.IsNullOrWhiteSpace(countryCode))
        {
            var country = countryCode.Trim().ToUpperInvariant();
            query = query.Where(communityEvent => communityEvent.Location.CountryCode == country);
        }

        if (neighborhoodAreaId is { } areaId && areaId != Guid.Empty)
        {
            query = query.Where(
                communityEvent => communityEvent.Location.NeighborhoodAreaId == areaId);
        }

        return await query
            .OrderBy(communityEvent => communityEvent.Schedule.StartsAt)
            .Take(Math.Clamp(limit, 1, 500))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CommunityEvent>> FindWithinBoxAsync(
        double minLatitude,
        double maxLatitude,
        double minLongitude,
        double maxLongitude,
        DateTimeOffset asOf,
        int limit,
        CancellationToken cancellationToken = default)
    {
        // A bounding box rather than a haversine, and the caller refines. Location owns distance
        // arithmetic; this narrows to an indexed range so the refinement is over tens of rows
        // rather than the whole table.
        return await Events
            .AsNoTracking()
            .Where(communityEvent =>
                communityEvent.Status == EventStatus.Published
                && communityEvent.Visibility == EventVisibility.Public
                && communityEvent.ModerationState != CommunityLifeModerationState.Hidden
                && communityEvent.ModerationState != CommunityLifeModerationState.Suspended
                && communityEvent.Schedule.EndsAt >= asOf
                && communityEvent.Venue.Latitude != null
                && communityEvent.Venue.Longitude != null
                && communityEvent.Venue.Latitude >= minLatitude
                && communityEvent.Venue.Latitude <= maxLatitude
                && communityEvent.Venue.Longitude >= minLongitude
                && communityEvent.Venue.Longitude <= maxLongitude)
            .OrderBy(communityEvent => communityEvent.Schedule.StartsAt)
            .Take(Math.Clamp(limit, 1, 200))
            .ToListAsync(cancellationToken);
    }

    // ── Occurrences ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// One series' dates, soonest first, windowed and paged.
    /// <para>
    /// <b>The cursor is the sequence number, not a timestamp, and that is the whole point.</b>
    /// Within a series the sequence is unique and orders identically to the start time, so the
    /// keyset predicate is a plain integer comparison — no tie group, no boundary identifiers to
    /// carry, and no possibility of the defect a timestamp cursor has when an organizer moves two
    /// occurrences onto the same evening. The composite index over series and sequence serves the
    /// predicate and the ordering together.
    /// </para>
    /// </summary>
    public async Task<CursorPagedResult<CommunityEvent>> FindOccurrencesAsync(
        EventSeriesId seriesId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        bool includeNonPublic,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesId);

        var query = Events
            .AsNoTracking()
            .Where(communityEvent => communityEvent.SeriesId == seriesId);

        if (!includeNonPublic)
        {
            query = query.Where(communityEvent =>
                communityEvent.Status != EventStatus.Draft
                && communityEvent.Status != EventStatus.Archived
                && communityEvent.ModerationState != CommunityLifeModerationState.Hidden
                && communityEvent.ModerationState != CommunityLifeModerationState.Suspended);
        }

        // The window is against the end time on the left and the start time on the right, so an
        // occurrence already running when the window opens is in it. A calendar that dropped the
        // thing happening right now would be wrong in the one case somebody is looking at it.
        if (from is { } windowStart)
        {
            query = query.Where(communityEvent => communityEvent.Schedule.EndsAt >= windowStart);
        }

        if (to is { } windowEnd)
        {
            query = query.Where(communityEvent => communityEvent.Schedule.StartsAt <= windowEnd);
        }

        if (CommunityLifeCursor.Parse(cursor)?.AsNumber() is { } boundary)
        {
            var after = (int)boundary;

            query = query.Where(communityEvent => communityEvent.SeriesSequence > after);
        }

        var page = Math.Clamp(limit, 1, CommunityLifeLimits.MaxPageSize);

        var rows = await query
            .OrderBy(communityEvent => communityEvent.SeriesSequence)
            .Take(page + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > page;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? CommunityLifeCursor
                .FromNumber(rows[^1].SeriesSequence ?? 0, rows[^1].Slug.Value)
                .Encode()
            : null;

        return CursorPagedResult<CommunityEvent>.Create(rows, next, hasMore);
    }

    public async Task<IReadOnlyList<CommunityEvent>> FindOccurrencesToRewriteAsync(
        EventSeriesId seriesId,
        int fromSequence,
        DateTimeOffset notBefore,
        bool includeExceptions,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesId);

        var query = Events
            .Where(communityEvent => communityEvent.SeriesId == seriesId
                && communityEvent.SeriesSequence >= fromSequence
                && communityEvent.Schedule.StartsAt >= notBefore
                && communityEvent.Status != EventStatus.Cancelled
                && communityEvent.Status != EventStatus.Completed
                && communityEvent.Status != EventStatus.Archived);

        if (!includeExceptions)
        {
            query = query.Where(communityEvent => !communityEvent.IsSeriesException);
        }

        return await query
            .OrderBy(communityEvent => communityEvent.SeriesSequence)
            .Take(Math.Clamp(limit, 1, CommunityLifeLimits.MaxSeriesOccurrences))
            .ToListAsync(cancellationToken);
    }

    public async Task<CommunityEvent?> FindNextOccurrenceAsync(
        EventSeriesId seriesId,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesId);

        return await Events
            .AsNoTracking()
            .Where(communityEvent => communityEvent.SeriesId == seriesId
                && communityEvent.Schedule.EndsAt >= asOf
                && communityEvent.Status != EventStatus.Cancelled
                && communityEvent.Status != EventStatus.Archived)
            .OrderBy(communityEvent => communityEvent.Schedule.StartsAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>
    /// The next date for each of several series.
    /// <para>
    /// <b>Two queries whatever the page size, and neither is unbounded.</b> The first groups to the
    /// earliest upcoming start per series — a group-by over an indexed column, which every provider
    /// translates. The second fetches the rows sitting on those instants. Ordering inside a group is
    /// what SQLite translates worst, so the ordering is expressed as a minimum instead.
    /// </para>
    /// <para>
    /// Two series whose next dates fall on the same instant bring back one extra row each; the
    /// in-memory pick discards it. Grouping is on the identifier property itself and never on
    /// <c>.Value</c> — a value-converted member access compiles and fails at runtime.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<EventSeriesId, CommunityEvent>> FindNextOccurrencesAsync(
        IReadOnlyCollection<EventSeriesId> seriesIds,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesIds);

        var wanted = seriesIds.Distinct().ToList();

        if (wanted.Count == 0)
        {
            return new Dictionary<EventSeriesId, CommunityEvent>();
        }

        var live = Events
            .AsNoTracking()
            .Where(communityEvent => communityEvent.SeriesId != null
                && wanted.Contains(communityEvent.SeriesId)
                && communityEvent.Schedule.EndsAt >= asOf
                && communityEvent.Status != EventStatus.Cancelled
                && communityEvent.Status != EventStatus.Archived);

        var earliest = await live
            .GroupBy(communityEvent => communityEvent.SeriesId)
            .Select(group => new
            {
                SeriesId = group.Key,
                StartsAt = group.Min(communityEvent => communityEvent.Schedule.StartsAt)
            })
            .ToListAsync(cancellationToken);

        if (earliest.Count == 0)
        {
            return new Dictionary<EventSeriesId, CommunityEvent>();
        }

        var starts = earliest.Select(row => row.StartsAt).Distinct().ToList();

        var rows = await live
            .Where(communityEvent => starts.Contains(communityEvent.Schedule.StartsAt))
            .ToListAsync(cancellationToken);

        var byStart = earliest.ToDictionary(row => row.SeriesId!, row => row.StartsAt);
        var next = new Dictionary<EventSeriesId, CommunityEvent>();

        foreach (var row in rows)
        {
            if (row.SeriesId is { } seriesId
                && byStart.TryGetValue(seriesId, out var startsAt)
                && row.Schedule.StartsAt == startsAt)
            {
                next.TryAdd(seriesId, row);
            }
        }

        return next;
    }

    /// <summary>
    /// The ordinals already written, including cancelled and archived ones.
    /// <para>
    /// <b>Deleted rows are counted too</b>, through <c>IgnoreQueryFilters</c>, and that is what
    /// makes expansion idempotent rather than nearly idempotent: a soft-deleted occurrence still
    /// holds its slug and its sequence, and regenerating the date would collide with both.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<int>> FindOccurrenceSequencesAsync(
        EventSeriesId seriesId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesId);

        return await Events
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(communityEvent => communityEvent.SeriesId == seriesId
                && communityEvent.SeriesSequence != null)
            .Select(communityEvent => communityEvent.SeriesSequence!.Value)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountOccurrencesAsync(
        EventSeriesId seriesId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(seriesId);

        return await Events
            .AsNoTracking()
            .CountAsync(communityEvent => communityEvent.SeriesId == seriesId, cancellationToken);
    }

    public async Task<(int EventCount, int OrganizerCount)> CountFirstPublishedByOrganizersAsync(
        IReadOnlyCollection<Guid> organizerUserIds,
        DateTimeOffset from,
        DateTimeOffset until,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizerUserIds);

        if (organizerUserIds.Count == 0)
        {
            return (0, 0);
        }

        var organizers = organizerUserIds.Distinct().Select(UserId.From).ToList();

        var published = await Events
            .AsNoTracking()
            .Where(communityEvent => organizers.Contains(communityEvent.OrganizerUserId)
                && communityEvent.FirstPublishedAt != null
                && communityEvent.FirstPublishedAt >= from
                && communityEvent.FirstPublishedAt < until)
            .Select(communityEvent => communityEvent.OrganizerUserId)
            .ToListAsync(cancellationToken);

        return (published.Count, published.Distinct().Count());
    }

    public async Task AddAsync(
        CommunityEvent communityEvent, CancellationToken cancellationToken = default) =>
        await Events.AddAsync(communityEvent, cancellationToken);

    public void Update(CommunityEvent communityEvent) => Events.Update(communityEvent);

    // ── Query composition ───────────────────────────────────────────────────────────────────

    [SuppressMessage(
        "Globalization", "CA1304:Specify CultureInfo",
        Justification =
            "The ToLower calls are inside an expression tree. They are never executed in .NET: EF "
            + "translates them to SQL lower(), which runs under the database collation. There is no "
            + "CultureInfo to specify, and passing one would fail to translate.")]
    [SuppressMessage(
        "Globalization", "CA1311:Specify a culture or use an invariant version",
        Justification = "See CA1304. ToLowerInvariant translates identically; neither is executed.")]
    [SuppressMessage(
        "Globalization", "CA1862:Use the StringComparison method overloads",
        Justification =
            "The StringComparison overloads have no translation on either provider, so they would "
            + "move the search into memory. Lowering both sides is what makes the match "
            + "case-insensitive on Postgres, where = is not.")]
    private static IQueryable<CommunityEvent> Apply(
        IQueryable<CommunityEvent> query, EventQueryFilter filter)
    {
        if (!filter.IncludeNonPublic)
        {
            query = query.Where(communityEvent =>
                (communityEvent.Status == EventStatus.Published
                    || communityEvent.Status == EventStatus.InProgress)
                && communityEvent.ModerationState != CommunityLifeModerationState.Hidden
                && communityEvent.ModerationState != CommunityLifeModerationState.Suspended);

            // Visibility is the part that needs the viewer. A public event is visible to everybody;
            // a members-only one only to somebody in the organizing organization, which the caller
            // resolves once into ViewerOrganizationIds rather than joining the membership table on
            // every row.
            if (filter.ViewerOrganizationIds is { Count: > 0 })
            {
                var memberOf = filter.ViewerOrganizationIds.ToList();

                query = query.Where(communityEvent =>
                    communityEvent.Visibility == EventVisibility.Public
                    || (communityEvent.Visibility == EventVisibility.MembersOnly
                        && communityEvent.OrganizationId != null
                        && memberOf.Contains(communityEvent.OrganizationId)));
            }
            else
            {
                query = query.Where(
                    communityEvent => communityEvent.Visibility == EventVisibility.Public);
            }
        }
        else if (filter.ViewerUserId is { } viewer && filter.OrganizationId is null)
        {
            // "Include non-public" on a member's own listing means "include mine", not
            // "include everybody's". A staff listing passes no viewer.
            query = query.Where(communityEvent => communityEvent.OrganizerUserId == viewer);
        }

        if (!filter.IncludePast)
        {
            var now = DateTimeOffset.UtcNow;
            query = query.Where(communityEvent => communityEvent.Schedule.EndsAt >= now);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(communityEvent => statuses.Contains(communityEvent.Status));
        }

        if (filter.CategoryKeys is { Count: > 0 })
        {
            var keys = filter.CategoryKeys
                .Select(Organization.NormalizeKey)
                .Where(key => key is not null)
                .ToList();

            query = query.Where(communityEvent => keys.Contains(communityEvent.CategoryKey));
        }

        if (filter.OrganizationId is { } organizationId)
        {
            query = query.Where(
                communityEvent => communityEvent.OrganizationId == organizationId);
        }

        if (filter.OrganizerUserId is { } organizerUserId)
        {
            query = query.Where(
                communityEvent => communityEvent.OrganizerUserId == organizerUserId);
        }

        if (filter.CommunityId is { } communityId)
        {
            query = query.Where(communityEvent => communityEvent.CommunityId == communityId);
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(communityEvent => communityEvent.Location.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            // Lowered on both sides (14 Oct 2026): Postgres compares text case-sensitively and the
            // SQLite tests do not.
            var city = filter.CityName.Trim().ToLowerInvariant();
            query = query.Where(communityEvent =>
                communityEvent.Location.CityName != null
                && communityEvent.Location.CityName.ToLower() == city);
        }

        if (filter.NeighborhoodAreaId is { } areaId && areaId != Guid.Empty)
        {
            query = query.Where(
                communityEvent => communityEvent.Location.NeighborhoodAreaId == areaId);
        }

        if (!string.IsNullOrWhiteSpace(filter.AreaPathPrefix))
        {
            var prefix = filter.AreaPathPrefix;

            query = query.Where(communityEvent =>
                communityEvent.Location.AreaPath != null
                && communityEvent.Location.AreaPath.StartsWith(prefix));
        }

        if (filter.HasBounds)
        {
            var south = filter.BoundsSouth!.Value;
            var north = filter.BoundsNorth!.Value;
            var west = filter.BoundsWest!.Value;
            var east = filter.BoundsEast!.Value;

            // An event with no venue coordinate in the town a circle search named (14 Oct 2026).
            var unpinned = string.IsNullOrWhiteSpace(filter.UnpinnedCityName)
                ? null
                : filter.UnpinnedCityName.Trim().ToLowerInvariant();
            var keepUnpinned = unpinned is not null;

            // An online-only event has no venue coordinate and is excluded: a map viewport asks
            // what is happening in a rectangle, and a video call is not happening in one.
            //
            // Two branches rather than a captured bool, so each shape stays a plain pair of range
            // comparisons over the indexed columns.
            query = filter.BoundsCrossAntimeridian
                ? query.Where(communityEvent =>
                    (communityEvent.Venue.Latitude != null
                        && communityEvent.Venue.Longitude != null
                        && communityEvent.Venue.Latitude >= south
                        && communityEvent.Venue.Latitude <= north
                        && (communityEvent.Venue.Longitude >= west
                            || communityEvent.Venue.Longitude <= east))
                    || (keepUnpinned
                        && communityEvent.Venue.Latitude == null
                        && communityEvent.Location.CityName != null
                        && communityEvent.Location.CityName.ToLower() == unpinned))
                : query.Where(communityEvent =>
                    (communityEvent.Venue.Latitude != null
                        && communityEvent.Venue.Longitude != null
                        && communityEvent.Venue.Latitude >= south
                        && communityEvent.Venue.Latitude <= north
                        && communityEvent.Venue.Longitude >= west
                        && communityEvent.Venue.Longitude <= east)
                    || (keepUnpinned
                        && communityEvent.Venue.Latitude == null
                        && communityEvent.Location.CityName != null
                        && communityEvent.Location.CityName.ToLower() == unpinned));
        }

        if (filter.StartsAfter is { } after)
        {
            query = query.Where(communityEvent => communityEvent.Schedule.StartsAt >= after);
        }

        if (filter.StartsBefore is { } before)
        {
            query = query.Where(communityEvent => communityEvent.Schedule.StartsAt <= before);
        }

        if (filter.FreeOnly == true)
        {
            query = query.Where(communityEvent => communityEvent.Audience.IsFree);
        }

        if (filter.FamilyFriendlyOnly == true)
        {
            query = query.Where(communityEvent => communityEvent.Audience.IsFamilyFriendly);
        }

        if (filter.ChildFriendlyOnly == true)
        {
            query = query.Where(communityEvent => communityEvent.Audience.IsChildFriendly);
        }

        if (filter.StudentOnly == true)
        {
            query = query.Where(communityEvent => communityEvent.Audience.IsStudentFocused);
        }

        if (filter.FeaturedOnly == true)
        {
            query = query.Where(communityEvent => communityEvent.IsFeatured);
        }

        if (filter.AttendanceMode is { } mode)
        {
            query = query.Where(communityEvent => communityEvent.AttendanceMode == mode);
        }

        if (filter.HasPlacesRemaining == true)
        {
            query = query.Where(communityEvent =>
                communityEvent.Capacity.Maximum == null
                || communityEvent.Capacity.GoingCount < communityEvent.Capacity.Maximum);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();

            query = query.Where(communityEvent =>
                communityEvent.Title.Value.Contains(term)
                || communityEvent.Description.Summary.Contains(term)
                || (communityEvent.Venue.Name != null
                    && communityEvent.Venue.Name.Contains(term)));
        }

        return query;
    }

    /// <summary>
    /// Orders the query and applies the continuation.
    /// <para>
    /// Every ordering ends with the slug, which is the tie-break: unique, indexed and a string,
    /// because the primary key is a value-converted identifier EF cannot compare in SQL. Without a
    /// stable tie-break, two events published in the same millisecond swap places between pages and
    /// one of them is never shown.
    /// </para>
    /// <para>
    /// <b>CA1309 is suppressed across the method, and it has to be.</b> The analyser wants
    /// <c>StringComparison.Ordinal</c>, and EF cannot translate that overload — the query would fall
    /// back to client evaluation, which for a cursor predicate means loading the table to find page
    /// two. B8 through B11 carry the same suppression for the same reason.
    /// </para>
    /// </summary>
#pragma warning disable CA1309
    private static IQueryable<CommunityEvent> ApplyOrderAndCursor(
        IQueryable<CommunityEvent> query,
        EventQueryFilter filter,
        CommunityLifeCursor? cursor)
    {
        switch (filter.SortOrder)
        {
            case EventSortOrder.Newest:
            {
                if (cursor?.AsTimestamp() is { } boundary)
                {
                    query = query.Where(communityEvent =>
                        communityEvent.PublishedAt < boundary
                        || (communityEvent.PublishedAt == boundary
                            && string.Compare(communityEvent.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(communityEvent => communityEvent.PublishedAt)
                    .ThenBy(communityEvent => communityEvent.Slug.Value);
            }

            case EventSortOrder.MostPopular:
            {
                if (cursor?.AsNumber() is { } value)
                {
                    var count = (int)value;

                    query = query.Where(communityEvent =>
                        communityEvent.Capacity.GoingCount < count
                        || (communityEvent.Capacity.GoingCount == count
                            && string.Compare(communityEvent.Slug.Value, cursor!.LastKey) > 0));
                }

                return query
                    .OrderByDescending(communityEvent => communityEvent.Capacity.GoingCount)
                    .ThenBy(communityEvent => communityEvent.Slug.Value);
            }

            // Nearest and Relevance are resolved above this layer — the geographic scope narrows the
            // query and the ranking registry orders what comes back — so both fall through to the
            // schedule ordering, which is the only one that can be paged in the database.
            case EventSortOrder.Soonest:
            case EventSortOrder.Nearest:
            case EventSortOrder.Relevance:
            default:
            {
                if (cursor?.AsTimestamp() is { } boundary)
                {
                    query = query.Where(communityEvent =>
                        communityEvent.Schedule.StartsAt > boundary
                        || (communityEvent.Schedule.StartsAt == boundary
                            && string.Compare(communityEvent.Slug.Value, cursor!.LastKey) > 0));
                }

                // Ascending: an event list is a plan, and the thing you can still get to comes
                // first. This is the module's one departure from every other catalogue.
                return query
                    .OrderBy(communityEvent => communityEvent.Schedule.StartsAt)
                    .ThenBy(communityEvent => communityEvent.Slug.Value);
            }
        }
    }
#pragma warning restore CA1309

    private static string NextCursor(CommunityEvent last, EventSortOrder sortOrder) =>
        sortOrder switch
        {
            EventSortOrder.Newest => CommunityLifeCursor
                .FromTimestamp(last.PublishedAt ?? last.CreatedAt, last.Slug.Value)
                .Encode(),
            EventSortOrder.MostPopular => CommunityLifeCursor
                .FromNumber(last.Capacity.GoingCount, last.Slug.Value)
                .Encode(),
            _ => CommunityLifeCursor
                .FromTimestamp(last.Schedule.StartsAt, last.Slug.Value)
                .Encode()
        };
}
