using Diaspora.Modules.Guides.Domain.Enums;
using Diaspora.Modules.Guides.Domain.Identifiers;
using Diaspora.Modules.Guides.Domain.Intelligence;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Guides.Persistence.Repositories;

/// <summary>
/// EF Core repository for official notices.
/// <para>
/// <b>One filter, one query builder, every listing.</b> <see cref="Compose"/> is the single place a
/// <see cref="NoticeQueryFilter"/> becomes predicates, and every read on this class goes through it —
/// which is what makes a count and the listing behind it incapable of disagreeing, and what makes a
/// contributed search engine subject to identical visibility rules.
/// </para>
/// <para>
/// <b>Paging is cursor-based with an explicit tie-break.</b> A strict comparison on the sort key alone
/// silently drops every row sharing the boundary — and notices are published in batches by an
/// editorial team, so ties are the norm rather than the edge. <c>CursorBoundary</c> carries the
/// identifiers already returned and the query excludes them; B9 measured what happens without it.
/// </para>
/// </summary>
public sealed class OfficialNoticeRepository : IOfficialNoticeRepository
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IClock _clock;

    public OfficialNoticeRepository(DiasporaDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    private DbSet<OfficialNotice> Notices => _dbContext.Set<OfficialNotice>();

    public async Task<OfficialNotice?> GetAsync(
        OfficialNoticeId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Notices)
            .FirstOrDefaultAsync(notice => notice.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<OfficialNotice?> GetIncludingArchivedAsync(
        OfficialNoticeId id, CancellationToken cancellationToken = default) =>
        await FullGraph(Notices.IgnoreQueryFilters())
            .FirstOrDefaultAsync(notice => notice.Id == id, cancellationToken);

    public async Task<CursorPagedResult<OfficialNotice>> ListAsync(
        NoticeQueryFilter filter, string? cursor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Compose(filter);
        var parsed = GuideCursor.Parse(cursor);

        query = ApplyCursor(query, filter.Sort, parsed);

        var limit = filter.EffectiveLimit;

        var items = await Order(query, filter.Sort)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = items.Count > limit;

        if (hasMore)
        {
            items.RemoveAt(items.Count - 1);
        }

        var next = hasMore && items.Count > 0
            ? NextCursor(items, filter.Sort, parsed)
            : null;

        return CursorPagedResult<OfficialNotice>.Create(items, next, hasMore);
    }

    public async Task<IReadOnlyList<OfficialNotice>> TakeAsync(
        NoticeQueryFilter filter, int limit, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Order(Compose(filter), filter.Sort)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// An exact count under the same filter the listing pages with.
    /// <para>
    /// Exact rather than "at least a page", because the number is read by an editorial team deciding
    /// what needs attention, and "20+" is technically true and useless to them.
    /// </para>
    /// </summary>
    public async Task<int> CountAsync(
        NoticeQueryFilter filter, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        return await Compose(filter).CountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OfficialNotice>> GetByIdsAsync(
        IReadOnlyCollection<OfficialNoticeId> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        if (ids.Count == 0)
        {
            return [];
        }

        var distinct = ids.Distinct().ToList();

        return await FullGraph(Notices.AsNoTracking())
            .Where(notice => distinct.Contains(notice.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AnyForCategoryAsync(
        string categoryKey, CancellationToken cancellationToken = default)
    {
        var normalized = IntelligenceNormalization.Key(categoryKey);

        // IgnoreQueryFilters: an archived notice filed under the category is still a reason not to
        // retire it, because restoring the notice would strand it under a category that is gone.
        return normalized is not null
            && await Notices
                .IgnoreQueryFilters()
                .AnyAsync(notice => notice.CategoryKey == normalized, cancellationToken);
    }

    public async Task<bool> AnyPublishedForAuthorityAsync(
        IssuingAuthorityId authorityId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorityId);

        return await Notices.AnyAsync(
            notice => notice.AuthorityId == authorityId
                && (notice.Status == NoticeStatus.Published
                    || notice.Status == NoticeStatus.Superseded),
            cancellationToken);
    }

    public async Task<IReadOnlyList<OfficialNotice>> ListExpiredUnannouncedAsync(
        DateTimeOffset asOf, int limit, CancellationToken cancellationToken = default) =>
        await Notices
            .Where(notice =>
                notice.Status == NoticeStatus.Published
                && notice.ExpiredAt == null
                && notice.ExpiresAt != null
                && notice.ExpiresAt <= asOf)
            .OrderBy(notice => notice.ExpiresAt)
            .Take(Math.Max(1, limit))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// How many current notices sit under each category key.
    /// <para>
    /// Grouped in the database rather than counted per category in a loop. A country page renders a
    /// chip per category with a badge on it, and a query per chip is the N+1 the whole dashboard
    /// design exists to avoid.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyDictionary<string, int>> CountByCategoryAsync(
        string? countryCode, DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        var country = IntelligenceNormalization.Country(countryCode);

        var query = Notices
            .AsNoTracking()
            .Where(notice =>
                notice.Status == NoticeStatus.Published
                && (notice.EffectiveFrom == null || notice.EffectiveFrom <= asOf)
                && (notice.ExpiresAt == null || notice.ExpiresAt > asOf));

        if (country is not null)
        {
            query = query.Where(notice => notice.CountryCode == country);
        }

        var grouped = await query
            .GroupBy(notice => notice.CategoryKey)
            .Select(group => new { Key = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        return grouped.ToDictionary(row => row.Key, row => row.Count, StringComparer.Ordinal);
    }

    public async Task AddAsync(
        OfficialNotice notice, CancellationToken cancellationToken = default) =>
        await Notices.AddAsync(notice, cancellationToken);

    /// <inheritdoc />
    public void Remove(OfficialNotice notice) => Notices.Remove(notice);

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Composition
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Turns a filter into a query. The single place any predicate on this table is written.
    /// </summary>
    private IQueryable<OfficialNotice> Compose(NoticeQueryFilter filter)
    {
        // Archived notices are outside the global filter, so including them needs the filter off —
        // and then the status predicate has to do the work the filter was doing.
        var query = filter.IncludeArchived && filter.ViewerIsEditor
            ? FullGraph(Notices.IgnoreQueryFilters().AsNoTracking())
            : FullGraph(Notices.AsNoTracking());

        query = ApplyVisibility(query, filter);
        query = ApplySubject(query, filter);
        query = ApplyClassification(query, filter);
        query = ApplyDates(query, filter);

        if (!string.IsNullOrWhiteSpace(filter.Text))
        {
            // Naive by design. Real relevance is the search seam's job, and a LIKE that pretended
            // otherwise would be the thing an OpenSearch adapter has to reproduce bug-for-bug.
            var text = filter.Text.Trim();

            query = query.Where(notice =>
                EF.Functions.Like(notice.Title, $"%{text}%")
                || EF.Functions.Like(notice.Summary, $"%{text}%"));
        }

        if (filter.NoticeIds.Count > 0)
        {
            var ids = filter.NoticeIds.Distinct().Select(OfficialNoticeId.From).ToList();

            query = query.Where(notice => ids.Contains(notice.Id));
        }

        return query;
    }

    private static IQueryable<OfficialNotice> ApplyVisibility(
        IQueryable<OfficialNotice> query, NoticeQueryFilter filter)
    {
        IEnumerable<NoticeStatus> statuses = filter.Statuses.Count > 0
            ? filter.Statuses
            : [NoticeStatus.Published, NoticeStatus.Superseded];

        // A reader may only ever see published and superseded notices, whatever the filter asks
        // for. Narrowing here rather than trusting the caller is what keeps a screen that forgot to
        // set its statuses from leaking the editorial queue.
        if (!filter.ViewerIsEditor)
        {
            statuses = statuses.Where(
                status => status is NoticeStatus.Published or NoticeStatus.Superseded);
        }

        var effective = statuses.Distinct().ToList();

        return effective.Count == 0
            ? query.Where(_ => false)
            : query.Where(notice => effective.Contains(notice.Status));
    }

    private static IQueryable<OfficialNotice> ApplySubject(
        IQueryable<OfficialNotice> query, NoticeQueryFilter filter)
    {
        var country = IntelligenceNormalization.Country(filter.CountryCode);

        if (country is not null)
        {
            query = query.Where(notice => notice.CountryCode == country);
        }

        var category = IntelligenceNormalization.Key(filter.CategoryKey);

        if (category is not null)
        {
            query = query.Where(notice => notice.CategoryKey == category);
        }

        if (filter.CategoryKeys.Count > 0)
        {
            var keys = filter.CategoryKeys
                .Select(IntelligenceNormalization.Key)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal)
                .ToList();

            query = keys.Count == 0
                ? query.Where(_ => false)
                : query.Where(notice => keys.Contains(notice.CategoryKey));
        }

        if (filter.AuthorityId is { } authorityId)
        {
            query = query.Where(notice => notice.AuthorityId == authorityId);
        }

        var language = IntelligenceNormalization.Locale(filter.Language);

        if (language is not null)
        {
            query = query.Where(notice =>
                notice.Languages.Any(code => code.Code == language));
        }

        return query;
    }

    private static IQueryable<OfficialNotice> ApplyClassification(
        IQueryable<OfficialNotice> query, NoticeQueryFilter filter)
    {
        if (filter.Severities.Count > 0)
        {
            var severities = filter.Severities.Distinct().ToList();

            query = query.Where(notice => severities.Contains(notice.Severity));
        }

        if (filter.MinimumSeverity is { } minimum)
        {
            // On the rank, never on the enum: severity is stored as text, so `>=` on the enum would
            // be an alphabetical comparison and "anything High or worse" would silently include
            // Informational and exclude Critical.
            var floor = (int)minimum;

            query = query.Where(notice => notice.SeverityRank >= floor);
        }

        if (filter.VerifiedOnly || filter.OfficialOnly)
        {
            query = query.Where(
                notice => notice.VerificationState == NoticeVerificationState.Verified);
        }

        return query;
    }

    private IQueryable<OfficialNotice> ApplyDates(
        IQueryable<OfficialNotice> query, NoticeQueryFilter filter)
    {
        var asOf = filter.AsOf ?? _clock.UtcNow;

        if (filter.ActiveOnly)
        {
            // The predicate form of OfficialNotice.IsActiveAt. The aggregate's version is what a
            // handler asks; this is the same rule expressed where a provider can translate it, and
            // the two are asserted equivalent by the unit suite.
            query = query.Where(notice =>
                notice.Status == NoticeStatus.Published
                && (notice.EffectiveFrom == null || notice.EffectiveFrom <= asOf)
                && (notice.ExpiresAt == null || notice.ExpiresAt > asOf));
        }

        if (filter.ExpiredOnly)
        {
            query = query.Where(notice => notice.ExpiresAt != null && notice.ExpiresAt <= asOf);
        }

        if (filter.PublishedFrom is { } from)
        {
            query = query.Where(notice => notice.PublishedAt != null && notice.PublishedAt >= from);
        }

        if (filter.PublishedBefore is { } before)
        {
            query = query.Where(notice => notice.PublishedAt != null && notice.PublishedAt < before);
        }

        if (filter.EffectiveFrom is { } effective)
        {
            query = query.Where(
                notice => notice.EffectiveFrom != null && notice.EffectiveFrom >= effective);
        }

        return query;
    }

    // ════════════════════════════════════════════════════════════════════════════════════════
    // Ordering and paging
    // ════════════════════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Orders by the requested sort, always ending on the identifier.
    /// <para>
    /// The trailing <c>ThenBy</c> on the identifier is not decoration: without a total order, two
    /// rows with equal sort keys can come back in different orders on two calls, and cursor paging
    /// over an unstable order repeats and drops rows regardless of how correct the boundary is.
    /// Ordering on a value-converted identifier translates fine — only comparing one does not.
    /// </para>
    /// </summary>
    private static IQueryable<OfficialNotice> Order(
        IQueryable<OfficialNotice> query, NoticeSortOrder sort) =>
        sort switch
        {
            NoticeSortOrder.Newest => query
                .OrderByDescending(notice => notice.PublishedAt)
                .ThenByDescending(notice => notice.Id),

            NoticeSortOrder.Oldest => query
                .OrderBy(notice => notice.PublishedAt)
                .ThenBy(notice => notice.Id),

            NoticeSortOrder.EffectiveSoonest => query
                .OrderBy(notice => notice.EffectiveFrom)
                .ThenBy(notice => notice.Id),

            NoticeSortOrder.ExpiringSoonest => query
                .OrderBy(notice => notice.ExpiresAt)
                .ThenBy(notice => notice.Id),

            NoticeSortOrder.MostViewed => query
                .OrderByDescending(notice => notice.ViewCount)
                .ThenByDescending(notice => notice.Id),

            // Priority: a briefing rather than a chronology. Severity leads, then the editor's pin,
            // then recency — which is the order somebody scanning a country page needs. On the rank
            // rather than the enum: the enum is stored as text and would sort alphabetically.
            _ => query
                .OrderByDescending(notice => notice.SeverityRank)
                .ThenByDescending(notice => notice.Priority)
                .ThenByDescending(notice => notice.PublishedAt)
                .ThenByDescending(notice => notice.Id)
        };

    /// <summary>
    /// Resumes after the cursor's boundary, excluding the identifiers already served at it.
    /// <para>
    /// <b>The priority sort pages on publication time even though it orders on severity first</b>,
    /// and that is a deliberate, documented compromise. A keyset over four columns cannot be written
    /// against a value-converted identifier, and a cursor that carried only the severity would tie
    /// hundreds of rows together. Publication time is near-unique, so the boundary is exact for
    /// almost every page; where it is not, the tie-break identifiers close the gap. The cost is that
    /// a caller paging deep into a priority listing sees the tail ordered by time within a severity
    /// band, which is what they were reading anyway.
    /// </para>
    /// </summary>
    private static IQueryable<OfficialNotice> ApplyCursor(
        IQueryable<OfficialNotice> query, NoticeSortOrder sort, GuideCursor? cursor)
    {
        if (cursor is null)
        {
            return query;
        }

        var tie = CursorBoundary.Exclusion(cursor.LastKey, OfficialNoticeId.From);
        var seen = tie.Ids;
        var exhausted = tie.IsExhausted;

        if (sort == NoticeSortOrder.MostViewed)
        {
            if (cursor.AsNumber() is not { } views)
            {
                return query;
            }

            var boundary = (long)views;

            return query.Where(notice =>
                notice.ViewCount < boundary
                || (notice.ViewCount == boundary && !exhausted && !seen.Contains(notice.Id)));
        }

        if (cursor.AsTimestamp() is not { } after)
        {
            return query;
        }

        return sort switch
        {
            NoticeSortOrder.Oldest => query.Where(notice =>
                notice.PublishedAt > after
                || (notice.PublishedAt == after && !exhausted && !seen.Contains(notice.Id))),

            NoticeSortOrder.EffectiveSoonest => query.Where(notice =>
                notice.EffectiveFrom > after
                || (notice.EffectiveFrom == after && !exhausted && !seen.Contains(notice.Id))),

            NoticeSortOrder.ExpiringSoonest => query.Where(notice =>
                notice.ExpiresAt > after
                || (notice.ExpiresAt == after && !exhausted && !seen.Contains(notice.Id))),

            _ => query.Where(notice =>
                notice.PublishedAt < after
                || (notice.PublishedAt == after && !exhausted && !seen.Contains(notice.Id)))
        };
    }

    private static string NextCursor(
        IReadOnlyList<OfficialNotice> items, NoticeSortOrder sort, GuideCursor? previous)
    {
        if (sort == NoticeSortOrder.MostViewed)
        {
            return GuideCursor.FromNumber(
                items[^1].ViewCount,
                CursorBoundary.Key(
                    items,
                    notice => notice.ViewCount,
                    notice => notice.Id.Value,
                    previous?.LastKey,
                    previous?.AsNumber() is { } value ? (long)value : null)).Encode();
        }

        var key = SortKey(sort);
        var boundary = key(items[^1]) ?? DateTimeOffset.MinValue;

        return GuideCursor.FromTimestamp(
            boundary,
            CursorBoundary.Key(
                items,
                notice => key(notice) ?? DateTimeOffset.MinValue,
                notice => notice.Id.Value,
                previous?.LastKey,
                previous?.AsTimestamp())).Encode();
    }

    private static Func<OfficialNotice, DateTimeOffset?> SortKey(NoticeSortOrder sort) =>
        sort switch
        {
            NoticeSortOrder.EffectiveSoonest => notice => notice.EffectiveFrom,
            NoticeSortOrder.ExpiringSoonest => notice => notice.ExpiresAt,
            _ => notice => notice.PublishedAt
        };

    private static IQueryable<OfficialNotice> FullGraph(IQueryable<OfficialNotice> query) =>
        query
            .Include(notice => notice.Localizations)
            .Include(notice => notice.Attachments)
            .Include(notice => notice.References)
            .Include(notice => notice.Languages);
}
