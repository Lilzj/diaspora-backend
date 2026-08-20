using Diaspora.Modules.Marketplace.Domain.Enums;
using Diaspora.Modules.Marketplace.Domain.Identifiers;
using Diaspora.Modules.Marketplace.Domain.Policies;
using Diaspora.Modules.Marketplace.Domain.Reports;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Marketplace.Persistence.Repositories;

/// <summary>EF Core repository for listing reports.</summary>
public sealed class ListingReportRepository : IListingReportRepository
{
    private static readonly ListingReportStatus[] OpenStatuses =
        [ListingReportStatus.Open, ListingReportStatus.Reviewing];

    private readonly DiasporaDbContext _dbContext;

    public ListingReportRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<ListingReport> Reports => _dbContext.Set<ListingReport>();

    public async Task<ListingReport?> GetAsync(
        ListingReportId id, CancellationToken cancellationToken = default) =>
        await Reports.FirstOrDefaultAsync(report => report.Id == id, cancellationToken);

    public async Task<ListingReport?> FindAsync(
        MarketplaceListingId listingId,
        UserId reporterId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(reporterId);

        return await Reports.FirstOrDefaultAsync(
            report => report.ListingId == listingId && report.ReporterId == reporterId,
            cancellationToken);
    }

    public async Task<int> CountForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Reports.AsNoTracking()
            .CountAsync(report => report.ListingId == listingId, cancellationToken);
    }

    public async Task<CursorPagedResult<ListingReport>> QueryAsync(
        ListingReportStatus? status,
        ListingReportReason? reason,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Reports.AsNoTracking().AsQueryable();

        // No status means the queue's default: everything still needing a decision.
        query = status is { } value
            ? query.Where(report => report.Status == value)
            : query.Where(report => OpenStatuses.Contains(report.Status));

        if (reason is { } filterReason)
        {
            query = query.Where(report => report.Reason == filterReason);
        }

        var parsed = MarketplaceCursor.Parse(cursor);

        query = ApplyCursor(query, parsed);

        var effectiveLimit = Math.Clamp(limit, 1, MarketplaceLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? MarketplaceCursor.FromTimestamp(
                rows[^1].CreatedAt,
                CursorBoundary.Key(
                    rows,
                    report => report.CreatedAt,
                    report => report.Id.Value,
                    parsed?.LastKey,
                    parsed?.AsTimestamp())).Encode()
            : null;

        return CursorPagedResult<ListingReport>.Create(rows, next, hasMore);
    }

    public async Task<IReadOnlyList<ListingReport>> GetOpenForListingAsync(
        MarketplaceListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Reports
            .Where(report =>
                report.ListingId == listingId && OpenStatuses.Contains(report.Status))
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        ListingReport report, CancellationToken cancellationToken = default) =>
        await Reports.AddAsync(report, cancellationToken);

    /// <summary>
    /// Oldest first, deliberately. A moderation queue sorted newest-first is one where the oldest
    /// complaint is never reached.
    /// </summary>
    /// <summary>
    /// Oldest first — the moderation queue works from the back, so nothing waits forever — tie-broken
    /// by identifier through <see cref="CursorBoundary"/>.
    /// <para>
    /// The tie group matters more here than anywhere else in the module: reports arrive in bursts
    /// when one fraudulent listing is shared, so a strict timestamp comparison would drop exactly
    /// the queue a moderator most needs to see whole.
    /// </para>
    /// </summary>
    private static IQueryable<ListingReport> ApplyCursor(
        IQueryable<ListingReport> query, MarketplaceCursor? cursor)
    {
        if (cursor?.AsTimestamp() is { } value)
        {
            var tie = CursorBoundary.Exclusion(cursor.LastKey, ListingReportId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(report =>
                report.CreatedAt > value
                || (report.CreatedAt == value && !exhausted && !seen.Contains(report.Id)));
        }

        return query
            .OrderBy(report => report.CreatedAt)
            .ThenBy(report => report.Id);
    }
}
