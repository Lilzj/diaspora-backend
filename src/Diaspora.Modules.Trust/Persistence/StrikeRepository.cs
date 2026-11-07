using Diaspora.Contracts.Moderation.V1;
using Diaspora.Modules.Trust.Domain;
using Diaspora.Persistence;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Trust.Persistence;

/// <summary>A member's strikes.</summary>
public interface IStrikeRepository
{
    Task<IReadOnlyList<Strike>> ListForMemberAsync(UserId memberId, CancellationToken cancellationToken = default);

    Task<Strike?> GetForCaseAsync(ModerationItemId caseId, CancellationToken cancellationToken = default);

    Task AddAsync(Strike strike, CancellationToken cancellationToken = default);
}

public sealed class StrikeRepository : IStrikeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public StrikeRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<Strike> Strikes => _dbContext.Set<Strike>();

    public async Task<IReadOnlyList<Strike>> ListForMemberAsync(
        UserId memberId, CancellationToken cancellationToken = default) =>
        await Strikes.Where(strike => strike.MemberId == memberId).ToListAsync(cancellationToken);

    public async Task<Strike?> GetForCaseAsync(
        ModerationItemId caseId, CancellationToken cancellationToken = default) =>
        await Strikes.FirstOrDefaultAsync(strike => strike.CaseId == caseId, cancellationToken);

    public async Task AddAsync(Strike strike, CancellationToken cancellationToken = default) =>
        await Strikes.AddAsync(strike, cancellationToken);
}

/// <summary>
/// Answers every module's "may this member post here now" from their strikes (7 Nov 2026).
/// </summary>
public sealed class StrikePostingStanding : IPostingStanding
{
    private readonly DiasporaDbContext _dbContext;
    private readonly IClock _clock;

    public StrikePostingStanding(DiasporaDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public async Task<PostingBlock?> GetBlockAsync(
        Guid userId, string area, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(area))
        {
            return null;
        }

        var now = _clock.UtcNow;
        var member = UserId.From(userId);

        var strikes = await _dbContext.Set<Strike>()
            .AsNoTracking()
            .Where(strike => strike.MemberId == member && strike.RevokedAt == null)
            .ToListAsync(cancellationToken);

        var block = strikes
            .Where(strike => strike.BlocksPosting(area, now))
            .OrderByDescending(strike => strike.BlockedUntil)
            .FirstOrDefault();

        return block is null
            ? null
            : new PostingBlock(block.Area, block.BlockedUntil!.Value, block.Level.ToString());
    }
}
