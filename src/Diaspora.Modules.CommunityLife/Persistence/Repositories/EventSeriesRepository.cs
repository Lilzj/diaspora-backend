using Diaspora.Modules.CommunityLife.Domain.Enums;
using Diaspora.Modules.CommunityLife.Domain.Identifiers;
using Diaspora.Modules.CommunityLife.Domain.Organizations;
using Diaspora.Modules.CommunityLife.Domain.Policies;
using Diaspora.Modules.CommunityLife.Domain.Series;
using Diaspora.Modules.CommunityLife.Domain.ValueObjects;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.CommunityLife.Persistence.Repositories;

/// <summary>
/// EF Core repository for recurring series.
/// <para>
/// <b>Ordered newest-first, unlike every other read in this module.</b> An event list is a plan and
/// is ordered by when you can get to it; a series list is an organizer's inventory of the patterns
/// they run, and the one they set up most recently is the one they are looking for. The occurrences
/// keep the module's soonest-first ordering, because those are the things people attend.
/// </para>
/// <para>
/// The tie-break is the slug — unique, indexed and a string — for the reason every other page in
/// B12 uses it: the primary key is a value-converted identifier EF cannot compare in SQL.
/// </para>
/// </summary>
public sealed class EventSeriesRepository : IEventSeriesRepository
{
    private readonly DiasporaDbContext _dbContext;

    public EventSeriesRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<EventSeries> Series => _dbContext.Set<EventSeries>();

    public async Task<EventSeries?> GetAsync(
        EventSeriesId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Series.FirstOrDefaultAsync(series => series.Id == id, cancellationToken);
    }

    public async Task<EventSeries?> GetIncludingDeletedAsync(
        EventSeriesId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Series
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(series => series.Id == id, cancellationToken);
    }

    public async Task<EventSeries?> GetBySlugAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var normalized = CommunityLifeSlug.Slugify(slug);

        if (normalized is null)
        {
            return null;
        }

        return await Series
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(series => series.Slug.Value == normalized, cancellationToken);
    }

    public async Task<CursorPagedResult<EventSeries>> SearchAsync(
        EventSeriesQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Series.AsNoTracking(), filter);
        var cursor = CommunityLifeCursor.Parse(filter.Cursor);

        query = ApplyOrderAndCursor(query, cursor);

        var limit = Math.Clamp(filter.Limit, 1, CommunityLifeLimits.MaxPageSize);
        var rows = await query.Take(limit + 1).ToListAsync(cancellationToken);
        var hasMore = rows.Count > limit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? CommunityLifeCursor
                .FromTimestamp(rows[^1].CreatedAt, rows[^1].Slug.Value)
                .Encode()
            : null;

        return CursorPagedResult<EventSeries>.Create(rows, next, hasMore);
    }

    public async Task<bool> SlugExistsAsync(
        string slug, CancellationToken cancellationToken = default)
    {
        var normalized = CommunityLifeSlug.Slugify(slug);

        if (normalized is null)
        {
            return false;
        }

        return await Series
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(series => series.Slug.Value == normalized, cancellationToken);
    }

    public async Task<int> CountLiveByOrganizerAsync(
        UserId organizerUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(organizerUserId);

        return await Series
            .AsNoTracking()
            .CountAsync(
                series => series.OrganizerUserId == organizerUserId
                    && (series.Status == EventSeriesStatus.Draft
                        || series.Status == EventSeriesStatus.Published
                        || series.Status == EventSeriesStatus.Paused),
                cancellationToken);
    }

    /// <summary>
    /// The sweep's work list.
    /// <para>
    /// A series with no materialisation mark at all sorts first, because it has either just been
    /// created or just been rebased and has nothing on the calendar. After that, the furthest behind
    /// go first — a deployment catching up after an outage should serve the series whose next date
    /// is soonest, not the one whose identifier happens to sort low.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<EventSeries>> FindNeedingExpansionAsync(
        DateOnly horizon, int batchSize, CancellationToken cancellationToken = default) =>
        await Series
            .Where(series =>
                (series.Status == EventSeriesStatus.Draft
                    || series.Status == EventSeriesStatus.Published)
                && (series.MaterialisedThrough == null || series.MaterialisedThrough < horizon)
                && series.OccurrenceCount < CommunityLifeLimits.MaxSeriesOccurrences)
            .OrderBy(series => series.MaterialisedThrough == null ? 0 : 1)
            .ThenBy(series => series.MaterialisedThrough)
            .ThenBy(series => series.Slug.Value)
            .Take(Math.Clamp(batchSize, 1, 500))
            .ToListAsync(cancellationToken);

    public async Task AddAsync(EventSeries series, CancellationToken cancellationToken = default) =>
        await Series.AddAsync(series, cancellationToken);

    public void Update(EventSeries series) => Series.Update(series);

    // ── Query composition ───────────────────────────────────────────────────────────────────

    private static IQueryable<EventSeries> Apply(
        IQueryable<EventSeries> query, EventSeriesQueryFilter filter)
    {
        if (!filter.IncludeNonPublic)
        {
            query = query.Where(series =>
                series.Status == EventSeriesStatus.Published
                || series.Status == EventSeriesStatus.Paused);

            if (filter.ViewerOrganizationIds is { Count: > 0 })
            {
                var memberOf = filter.ViewerOrganizationIds.ToList();

                query = query.Where(series =>
                    series.Visibility == EventVisibility.Public
                    || (series.Visibility == EventVisibility.MembersOnly
                        && series.OrganizationId != null
                        && memberOf.Contains(series.OrganizationId)));
            }
            else
            {
                query = query.Where(series => series.Visibility == EventVisibility.Public);
            }
        }
        else if (filter.ViewerUserId is { } viewer && filter.OrganizationId is null)
        {
            // "Include non-public" on a member's own listing means "include mine", not
            // "include everybody's". A staff listing passes no viewer.
            query = query.Where(series => series.OrganizerUserId == viewer);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(series => statuses.Contains(series.Status));
        }

        if (filter.OrganizationId is { } organizationId)
        {
            query = query.Where(series => series.OrganizationId == organizationId);
        }

        if (filter.OrganizerUserId is { } organizerUserId)
        {
            query = query.Where(series => series.OrganizerUserId == organizerUserId);
        }

        if (filter.CategoryKeys is { Count: > 0 })
        {
            var keys = filter.CategoryKeys
                .Select(Organization.NormalizeKey)
                .Where(key => key is not null)
                .ToList();

            query = query.Where(series => keys.Contains(series.CategoryKey));
        }

        if (!string.IsNullOrWhiteSpace(filter.CountryCode))
        {
            var country = filter.CountryCode.Trim().ToUpperInvariant();
            query = query.Where(series => series.Location.CountryCode == country);
        }

        if (!string.IsNullOrWhiteSpace(filter.CityName))
        {
            var city = filter.CityName.Trim();
            query = query.Where(series => series.Location.CityName == city);
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();

            query = query.Where(series =>
                series.Title.Value.Contains(term)
                || series.Description.Summary.Contains(term));
        }

        return query;
    }

    /// <summary>
    /// Newest first, tie-broken on the slug.
    /// <para>
    /// <b>CA1309 is suppressed, and it has to be.</b> The analyser wants
    /// <c>StringComparison.Ordinal</c> and EF cannot translate that overload — the predicate would
    /// fall back to client evaluation, which for a cursor means loading the table to find page two.
    /// Every paged read in B8 through B12 carries the same suppression for the same reason.
    /// </para>
    /// </summary>
#pragma warning disable CA1309
    private static IQueryable<EventSeries> ApplyOrderAndCursor(
        IQueryable<EventSeries> query, CommunityLifeCursor? cursor)
    {
        if (cursor?.AsTimestamp() is { } boundary)
        {
            query = query.Where(series =>
                series.CreatedAt < boundary
                || (series.CreatedAt == boundary
                    && string.Compare(series.Slug.Value, cursor!.LastKey) > 0));
        }

        return query
            .OrderByDescending(series => series.CreatedAt)
            .ThenBy(series => series.Slug.Value);
    }
#pragma warning restore CA1309
}
