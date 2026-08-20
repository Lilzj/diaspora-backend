using Diaspora.Modules.Housing.Domain.Enums;
using Diaspora.Modules.Housing.Domain.Identifiers;
using Diaspora.Modules.Housing.Domain.Policies;
using Diaspora.Modules.Housing.Domain.Reports;
using Diaspora.Persistence.Paging;
using Diaspora.Persistence;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Pagination;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Housing.Persistence.Repositories;

/// <summary>
/// EF Core repository for housing reports. Every path is behind the moderation permission.
/// </summary>
public sealed class HousingReportRepository : IHousingReportRepository
{
    private static readonly HousingReportStatus[] OpenStates =
    [
        HousingReportStatus.Open,
        HousingReportStatus.UnderReview
    ];

    private readonly DiasporaDbContext _dbContext;

    public HousingReportRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<HousingReport> Reports => _dbContext.Set<HousingReport>();

    public async Task<HousingReport?> GetAsync(
        HousingReportId id, CancellationToken cancellationToken = default) =>
        await Reports.FirstOrDefaultAsync(report => report.Id == id, cancellationToken);

    public async Task<CursorPagedResult<HousingReport>> QueryAsync(
        HousingReportQueryFilter filter,
        string? cursor,
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var query = Apply(Reports.AsNoTracking(), filter);
        var parsed = HousingCursor.Parse(cursor);

        if (parsed?.AsTimestamp() is { } boundary)
        {
            // Strictly before the boundary instant, plus whatever remains of the tie group at
            // it. A plain `< boundary` drops every row sharing the instant, and the instant is
            // shared constantly: the persistence interceptor stamps CreatedAt, so everything
            // written in one unit of work ties. The identifier cannot be compared in SQL —
            // it is value-converted — so the cursor carries the ones already returned and the
            // query excludes them. CursorBoundary is the platform's answer to exactly this.
            var tie = CursorBoundary.Exclusion(parsed.LastKey, HousingReportId.From);
            var seen = tie.Ids;
            var exhausted = tie.IsExhausted;

            query = query.Where(report =>
                report.CreatedAt < boundary
                || (report.CreatedAt == boundary && !exhausted && !seen.Contains(report.Id)));
        }

        // Scam allegations first when the queue asks for it. A rental advance-fee fraud takes a
        // newcomer's deposit before they have a bank account or anyone to ask, and it is the one
        // report type where an hour in the queue matters.
        query = filter.ScamsFirst
            ? query
                .OrderByDescending(report => report.Reason == HousingReportReason.Scam)
                .ThenByDescending(report => report.CreatedAt)
                .ThenBy(report => report.Id)
            : query
                .OrderByDescending(report => report.CreatedAt)
                .ThenBy(report => report.Id);

        var effectiveLimit = Math.Clamp(limit, 1, HousingLimits.MaxPageSize);
        var rows = await query.Take(effectiveLimit + 1).ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectiveLimit;

        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var next = hasMore && rows.Count > 0
            ? HousingCursor.FromTimestamp(
                    rows[^1].CreatedAt,
                    CursorBoundary.Key(
                        rows,
                        row => row.CreatedAt,
                        row => row.Id.Value,
                        parsed?.LastKey,
                        parsed?.AsTimestamp()))
                .Encode()
            : null;

        return CursorPagedResult<HousingReport>.Create(rows, next, hasMore);
    }

    public async Task<bool> HasReportedAsync(
        HousingListingId listingId,
        UserId reportedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);
        ArgumentNullException.ThrowIfNull(reportedBy);

        return await Reports.AsNoTracking().AnyAsync(
            report => report.ListingId == listingId && report.ReportedBy == reportedBy,
            cancellationToken);
    }

    public async Task<int> CountForListingAsync(
        HousingListingId listingId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(listingId);

        return await Reports.AsNoTracking()
            .CountAsync(report => report.ListingId == listingId, cancellationToken);
    }

    public async Task<int> CountUpheldForLandlordAsync(
        LandlordProfileId landlordId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(landlordId);

        return await Reports.AsNoTracking().CountAsync(
            report => report.LandlordId == landlordId
                && report.Status == HousingReportStatus.Upheld,
            cancellationToken);
    }

    public async Task AddAsync(
        HousingReport report, CancellationToken cancellationToken = default) =>
        await Reports.AddAsync(report, cancellationToken);

    private static IQueryable<HousingReport> Apply(
        IQueryable<HousingReport> query, HousingReportQueryFilter filter)
    {
        if (filter.ListingId is { } listingId)
        {
            query = query.Where(report => report.ListingId == listingId);
        }

        if (filter.LandlordId is { } landlordId)
        {
            query = query.Where(report => report.LandlordId == landlordId);
        }

        if (filter.Statuses is { Count: > 0 })
        {
            var statuses = filter.Statuses.ToList();
            query = query.Where(report => statuses.Contains(report.Status));
        }

        if (filter.Reasons is { Count: > 0 })
        {
            var reasons = filter.Reasons.ToList();
            query = query.Where(report => reasons.Contains(report.Reason));
        }

        if (filter.OpenOnly)
        {
            query = query.Where(report => OpenStates.Contains(report.Status));
        }

        return query;
    }
}
