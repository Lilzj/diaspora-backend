using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Persistence;
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

    public async Task<IReadOnlyList<CreatorEarning>> ListForCreatorAsync(
        CreatorId creatorId,
        CreatorEarningState? state,
        CreatorEarningSource? source,
        int take,
        CancellationToken cancellationToken = default)
    {
        var query = Earnings.AsNoTracking().Where(earning => earning.CreatorId == creatorId);

        if (state is { } wantedState)
        {
            query = query.Where(earning => earning.State == wantedState);
        }

        if (source is { } wantedSource)
        {
            query = query.Where(earning => earning.Source == wantedSource);
        }

        return await query
            .OrderByDescending(earning => earning.OccurredAt)
            .Take(Math.Clamp(take, 1, 200))
            .ToListAsync(cancellationToken);
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
