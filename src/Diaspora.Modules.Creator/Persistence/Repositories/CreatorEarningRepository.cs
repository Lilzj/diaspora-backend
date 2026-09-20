using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Persistence.Repositories;

/// <summary>
/// Reads over one creator's earnings.
/// <para>
/// <b>Every total is summed by the database.</b> A revenue summary is four aggregates over a
/// creator's whole history, and the obvious implementation — load the rows and add them in C# —
/// works perfectly on the seeded deployment and degrades every month a creator earns anything. The
/// grouping below is the reason the summary stays one query regardless of how long somebody has
/// been earning.
/// </para>
/// <para>
/// <b>Currency is part of every grouping key.</b> A creator paid in two currencies has two totals
/// and no single number, and summing across them would produce a figure that is not money in any
/// currency. The caller decides what to do with more than one; this refuses to decide for it.
/// </para>
/// </summary>
public sealed class CreatorEarningRepository : ICreatorEarningRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorEarningRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorEarning> Earnings => _dbContext.Set<CreatorEarning>();

    public void Add(CreatorEarning earning) => Earnings.Add(earning);

    public Task<bool> ExistsAsync(
        string sourceReference, CancellationToken cancellationToken = default) =>
        Earnings.AsNoTracking()
            .AnyAsync(earning => earning.SourceReference == sourceReference, cancellationToken);

    public async Task<CreatorEarningPage> ListForCreatorAsync(
        CreatorId creatorId,
        CreatorEarningState? state,
        CreatorEarningSource? source,
        string? cursor,
        int take,
        CancellationToken cancellationToken = default)
    {
        var size = CreatorLimits.PageSize(take);

        var query = Earnings.AsNoTracking().Where(earning => earning.CreatorId == creatorId);

        if (state is { } wantedState)
        {
            query = query.Where(earning => earning.State == wantedState);
        }

        if (source is { } wantedSource)
        {
            query = query.Where(earning => earning.Source == wantedSource);
        }

        // **Exclusion by identity, not a comparison on the identifier.** `OccurredAt < cursor`
        // would skip every row sharing the boundary instant, and one purchase settling against two
        // subjects records both at the same instant routinely.
        //
        // The textbook repair — `key < @after OR (key == @after AND id < @lastId)` — cannot be
        // written against these identifiers, and the first two attempts here both proved it.
        // `earning.Id.Value < id` threw `The LINQ expression could not be translated`, so it was
        // rewritten as `EF.Property<Guid>(earning, nameof(Id)) < id`, which compiles, sorts
        // correctly, and throws `Object must implement IConvertible` on the first cursor: EF types
        // the parameter from the property and then hands the raw Guid to the identifier's value
        // converter, which cannot take one. That second version shipped, and was found only when
        // the sibling activity repository copied it and was given a test that pages against a
        // database. DSP3003 now refuses the shape.
        //
        // What works is the platform's own primitive: carry the identifiers already served at the
        // boundary and exclude them by set membership, which translates on every provider
        // (ADR-017).
        var parsed = CreatorCursor.Parse(cursor);
        var boundary = parsed?.AsTimestamp();

        if (boundary is { } after)
        {
            var exclusion = CursorBoundary.Exclusion(parsed!.BoundaryKey, CreatorEarningId.From);

            query = exclusion.IsExhausted
                ? query.Where(earning => earning.OccurredAt < after)
                : query.Where(earning =>
                    earning.OccurredAt < after
                    || (earning.OccurredAt == after && !exclusion.Ids.Contains(earning.Id)));
        }

        // Ordering by the identifier translates where comparing against one does not — an ORDER BY
        // is a column reference and needs no parameter — and it is what makes the boundary above
        // reproducible.
        var rows = await query
            .OrderByDescending(earning => earning.OccurredAt)
            .ThenByDescending(earning => earning.Id)
            .Take(size + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > size;
        var page = hasMore ? rows.Take(size).ToList() : rows;

        if (!hasMore || page.Count == 0)
        {
            return new CreatorEarningPage(page, null, false);
        }

        var next = CursorBoundary.Key(
            page,
            earning => earning.OccurredAt,
            earning => earning.Id.Value,
            parsed?.BoundaryKey,
            boundary);

        return new CreatorEarningPage(
            page, CreatorCursor.FromTimestamp(page[^1].OccurredAt, next).Encode(), true);
    }

    public async Task<IReadOnlyList<(CreatorEarningState State, string Currency, long MinorUnits, int Count)>>
        SumByStateAsync(
            CreatorId creatorId, DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        var query = Earnings.AsNoTracking().Where(earning => earning.CreatorId == creatorId);

        if (since is { } from)
        {
            query = query.Where(earning => earning.OccurredAt >= from);
        }

        var rows = await query
            .GroupBy(earning => new { earning.State, earning.Currency })
            .Select(group => new
            {
                group.Key.State,
                group.Key.Currency,
                MinorUnits = group.Sum(earning => earning.AmountMinorUnits),
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => (row.State, row.Currency, row.MinorUnits, row.Count))];
    }

    public async Task<IReadOnlyList<(CreatorEarningSource Source, string Currency, long MinorUnits, int Count)>>
        SumBySourceAsync(
            CreatorId creatorId, DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        var query = Earnings.AsNoTracking().Where(earning => earning.CreatorId == creatorId);

        if (since is { } from)
        {
            query = query.Where(earning => earning.OccurredAt >= from);
        }

        var rows = await query
            .GroupBy(earning => new { earning.Source, earning.Currency })
            .Select(group => new
            {
                group.Key.Source,
                group.Key.Currency,
                MinorUnits = group.Sum(earning => earning.AmountMinorUnits),
                Count = group.Count(),
            })
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => (row.Source, row.Currency, row.MinorUnits, row.Count))];
    }

    public async Task<IReadOnlyList<CreatorEarning>> ListClearableAsync(
        DateTimeOffset clearedBefore, int take, CancellationToken cancellationToken = default) =>
        await Earnings
            .Where(earning =>
                earning.State == CreatorEarningState.Pending && earning.OccurredAt <= clearedBefore)
            .OrderBy(earning => earning.OccurredAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(cancellationToken);
}
