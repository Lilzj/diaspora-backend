using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Financial.Persistence.Repositories;

/// <summary>EF Core repository for reward conversions.</summary>
public sealed class RewardConversionRepository : IRewardConversionRepository
{
    private readonly DiasporaDbContext _dbContext;

    public RewardConversionRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<RewardConversion> Conversions => _dbContext.Set<RewardConversion>();

    public void Add(RewardConversion conversion)
    {
        ArgumentNullException.ThrowIfNull(conversion);

        Conversions.Add(conversion);
    }

    public async Task<RewardConversion?> FindByIdempotencyKeyAsync(
        Guid memberUserId, string idempotencyKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey) || memberUserId == Guid.Empty)
        {
            return null;
        }

        var normalized = idempotencyKey.Trim();

        // Scoped to the member on both the staged and the persisted path. Matching on the
        // key alone answered one member's conversion request with another member's
        // conversion — its point count and its credited amount — and converted nothing for
        // the caller.
        var staged = Conversions.Local.FirstOrDefault(conversion =>
            string.Equals(conversion.IdempotencyKey, normalized, StringComparison.Ordinal)
            && conversion.MemberUserId == memberUserId);

        return staged ?? await Conversions.FirstOrDefaultAsync(
            conversion => conversion.IdempotencyKey == normalized
                && conversion.MemberUserId == memberUserId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<RewardConversion>> ListForMemberAsync(
        Guid memberUserId,
        DateTimeOffset? before,
        IReadOnlyList<Guid> excludeIds,
        int take,
        CancellationToken cancellationToken = default)
    {
        var conversions = Conversions.Where(conversion => conversion.MemberUserId == memberUserId);

        if (before is { } boundary)
        {
            conversions = conversions.Where(conversion => conversion.ConvertedAt <= boundary);
        }

        if (excludeIds is { Count: > 0 })
        {
            var excluded = excludeIds.Select(RewardConversionId.From).ToList();

            conversions = conversions.Where(conversion => !excluded.Contains(conversion.Id));
        }

        return await conversions
            .OrderByDescending(conversion => conversion.ConvertedAt)
            .Take(Math.Clamp(take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Points converted since a moment.
    /// <para>
    /// Queried rather than summed from the account's own entries, because a daily cap has to hold
    /// across two requests racing each other and the entry collection of one loaded aggregate only
    /// knows what that instance has seen.
    /// </para>
    /// </summary>
    public async Task<long> PointsConvertedSinceAsync(
        Guid memberUserId, DateTimeOffset since, CancellationToken cancellationToken = default) =>
        await Conversions
            .Where(conversion => conversion.MemberUserId == memberUserId
                && conversion.ConvertedAt >= since)
            .SumAsync(conversion => conversion.Points, cancellationToken);
}
