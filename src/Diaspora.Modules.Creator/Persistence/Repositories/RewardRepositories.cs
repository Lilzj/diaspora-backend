using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Policies;
using Diaspora.Modules.Creator.Domain.Referrals;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Domain.Rewards;
using Diaspora.Persistence;
using Diaspora.Persistence.Paging;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Persistence.Repositories;

/// <summary>
/// Reads and writes referral codes and referrals.
/// <para>
/// <b><see cref="CountsForCreatorAsync"/> counts the four states separately and never adds two of
/// them.</b> The register: "§18: Do not assume every signup is a qualifying referral" — and the
/// place that rule is most easily broken is a dashboard query that sums.
/// </para>
/// </summary>
public sealed class CreatorReferralRepository : ICreatorReferralRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorReferralRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorReferralCode> Codes => _dbContext.Set<CreatorReferralCode>();

    private DbSet<CreatorReferral> Referrals => _dbContext.Set<CreatorReferral>();

    public void AddCode(CreatorReferralCode code) => Codes.Add(code);

    public void AddReferral(CreatorReferral referral) => Referrals.Add(referral);

    public void UpdateReferral(CreatorReferral referral) => _dbContext.StageUpdate(referral);

    public async Task<CreatorReferralCode?> FindCodeForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        var staged = _dbContext.ChangeTracker
            .Entries<CreatorReferralCode>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(code => code.CreatorId == creatorId);

        if (staged is not null)
        {
            return staged;
        }

        return await Codes.FirstOrDefaultAsync(
            code => code.CreatorId == creatorId, cancellationToken);
    }

    public async Task<CreatorReferralCode?> FindCodeAsync(
        string code, CancellationToken cancellationToken = default)
    {
        var normalised = code.Trim().ToUpperInvariant();

        return await Codes.FirstOrDefaultAsync(
            entry => entry.Code == normalised && entry.IsActive, cancellationToken);
    }

    public async Task<CreatorReferral?> GetAsync(
        CreatorReferralId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Referrals.FirstOrDefaultAsync(
            referral => referral.Id == id, cancellationToken);
    }

    public async Task<CreatorReferral?> FindByInviteeAsync(
        Guid inviteeUserId, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<CreatorReferral>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(referral => referral.InviteeUserId == inviteeUserId);

        if (staged is not null)
        {
            return staged;
        }

        return await Referrals.FirstOrDefaultAsync(
            referral => referral.InviteeUserId == inviteeUserId, cancellationToken);
    }

    public async Task<ReferralCounts> CountsForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        var rows = await Referrals
            .AsNoTracking()
            .Where(referral => referral.ReferrerCreatorId == creatorId)
            .GroupBy(referral => referral.State)
            .Select(group => new
            {
                State = group.Key,
                Count = group.Count(),
                Points = group.Sum(referral => referral.RewardPoints)
            })
            .ToListAsync(cancellationToken);

        int CountOf(ReferralState state) =>
            rows.FirstOrDefault(row => row.State == state)?.Count ?? 0;

        // Invited counts everybody who was ever invited, because somebody who signed up was invited
        // first. Every other figure counts only rows currently in that state, so "signed up" never
        // silently includes "qualified" — the distinction the register insists on.
        var invited = rows.Sum(row => row.Count);

        var points = rows
            .Where(row => row.State is ReferralState.Qualified or ReferralState.Rewarded)
            .Sum(row => row.Points);

        return new ReferralCounts(
            invited,
            CountOf(ReferralState.SignedUp) + CountOf(ReferralState.Pending),
            CountOf(ReferralState.Qualified) + CountOf(ReferralState.Rewarded),
            CountOf(ReferralState.Rewarded),
            CountOf(ReferralState.Qualified),
            points);
    }

    public async Task<ReferralPage> ListForCreatorAsync(
        CreatorId creatorId,
        string? cursor,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        var size = CreatorLimits.PageSize(take);

        var query = Referrals
            .AsNoTracking()
            .Where(referral => referral.ReferrerCreatorId == creatorId);

        var parsed = CreatorCursor.Parse(cursor);
        var boundary = parsed?.AsTimestamp();

        if (boundary is { } after)
        {
            var exclusion = CursorBoundary.Exclusion(parsed!.BoundaryKey, CreatorReferralId.From);

            query = exclusion.IsExhausted
                ? query.Where(referral => referral.InvitedAt < after)
                : query.Where(referral =>
                    referral.InvitedAt < after
                    || (referral.InvitedAt == after && !exclusion.Ids.Contains(referral.Id)));
        }

        var rows = await query
            .OrderByDescending(referral => referral.InvitedAt)
            .ThenByDescending(referral => referral.Id)
            .Take(size + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > size;
        var page = hasMore ? rows.Take(size).ToList() : rows;

        if (!hasMore || page.Count == 0)
        {
            return new ReferralPage(page, null, false);
        }

        var next = CursorBoundary.Key(
            page,
            referral => referral.InvitedAt,
            referral => referral.Id.Value,
            parsed?.BoundaryKey,
            boundary);

        return new ReferralPage(
            page, CreatorCursor.FromTimestamp(page[^1].InvitedAt, next).Encode(), true);
    }

    public async Task<IReadOnlyList<CreatorReferral>> ListLapsedAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Referrals
            .Where(referral =>
                (referral.State == ReferralState.Invited
                    || referral.State == ReferralState.SignedUp
                    || referral.State == ReferralState.Pending)
                && referral.ExpiresAt <= asOf)
            .OrderBy(referral => referral.ExpiresAt)
            .Take(take)
            .ToListAsync(cancellationToken);
}

/// <summary>Reads and writes the sign-up bonus.</summary>
public sealed class SignupBonusRepository : ISignupBonusRepository
{
    private readonly DiasporaDbContext _dbContext;

    public SignupBonusRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<SignupBonus> Bonuses => _dbContext.Set<SignupBonus>();

    public void Add(SignupBonus bonus) => Bonuses.Add(bonus);

    public void Update(SignupBonus bonus) => _dbContext.StageUpdate(bonus);

    public async Task<SignupBonus?> FindForUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<SignupBonus>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(bonus => bonus.UserId == userId);

        if (staged is not null)
        {
            return staged;
        }

        return await Bonuses.FirstOrDefaultAsync(bonus => bonus.UserId == userId, cancellationToken);
    }

    public async Task<SignupBonus?> FindByAcknowledgeKeyAsync(
        CreatorId creatorId, string key, CancellationToken cancellationToken = default) =>
        await Bonuses.FirstOrDefaultAsync(
            bonus => bonus.CreatorId == creatorId && bonus.AcknowledgeIdempotencyKey == key, cancellationToken);
}

/// <summary>
/// Reads the reward catalogues and writes redemptions.
/// <para>
/// <b>The cap and the cooldown are counted in the database, not in memory.</b> §31 of the phase
/// brief requires the server to be the authority on eligibility; loading somebody's redemption
/// history to count it would make that authority a function of how much of it fitted in a page.
/// </para>
/// </summary>
public sealed class CreatorRewardRepository : ICreatorRewardRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorRewardRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorPointRule> Rules => _dbContext.Set<CreatorPointRule>();

    private DbSet<CreatorRewardDefinition> Rewards => _dbContext.Set<CreatorRewardDefinition>();

    private DbSet<CreatorRewardRedemption> Redemptions =>
        _dbContext.Set<CreatorRewardRedemption>();

    public void AddRule(CreatorPointRule rule) => Rules.Add(rule);

    public void AddReward(CreatorRewardDefinition reward) => Rewards.Add(reward);

    public void AddRedemption(CreatorRewardRedemption redemption) => Redemptions.Add(redemption);

    public void UpdateRedemption(CreatorRewardRedemption redemption) =>
        _dbContext.StageUpdate(redemption);

    public async Task<IReadOnlyList<CreatorPointRule>> ListRulesAsync(
        CancellationToken cancellationToken = default) =>
        await Rules
            .AsNoTracking()
            .Where(rule => rule.IsActive)
            .OrderBy(rule => rule.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<CreatorPointRule?> FindRuleByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<CreatorPointRule>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(rule => rule.Key == key);

        if (staged is not null)
        {
            return staged;
        }

        return await Rules.FirstOrDefaultAsync(rule => rule.Key == key, cancellationToken);
    }

    public async Task<CreatorPointRule?> FindRuleForActionAsync(
        string actionType, CancellationToken cancellationToken = default) =>
        await Rules
            .AsNoTracking()
            .FirstOrDefaultAsync(
                rule => rule.IsActive && rule.ActionType == actionType, cancellationToken);

    public async Task<IReadOnlyList<CreatorRewardDefinition>> ListRewardsAsync(
        CancellationToken cancellationToken = default) =>
        await Rewards
            .AsNoTracking()
            .Where(reward => reward.IsActive)
            .OrderBy(reward => reward.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<CreatorRewardDefinition?> GetRewardAsync(
        CreatorRewardId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Rewards.FirstOrDefaultAsync(reward => reward.Id == id, cancellationToken);
    }

    public async Task<CreatorRewardDefinition?> FindRewardByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<CreatorRewardDefinition>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(reward => reward.Key == key);

        if (staged is not null)
        {
            return staged;
        }

        return await Rewards.FirstOrDefaultAsync(reward => reward.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<CreatorRewardRedemption>> ListRedemptionsAsync(
        CreatorId creatorId, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        return await Redemptions
            .AsNoTracking()
            .Where(redemption => redemption.CreatorId == creatorId)
            .OrderByDescending(redemption => redemption.CreatedAt)
            .Take(CreatorLimits.PageSize(take))
            .ToListAsync(cancellationToken);
    }

    public async Task<CreatorRewardRedemption?> GetRedemptionAsync(
        CreatorRewardRedemptionId id, CancellationToken cancellationToken = default) =>
        await Redemptions.FirstOrDefaultAsync(redemption => redemption.Id == id, cancellationToken);

    public async Task<IReadOnlyList<CreatorRewardRedemption>> ListRedemptionsForReviewAsync(
        CreatorRewardState? state, int take, CancellationToken cancellationToken = default)
    {
        var query = Redemptions.AsNoTracking();

        if (state is { } wanted)
        {
            query = query.Where(redemption => redemption.State == wanted);
        }

        return await query
            .OrderBy(redemption => redemption.CreatedAt)
            .Take(CreatorLimits.PageSize(take))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountRedemptionsAsync(
        CreatorId creatorId, CreatorRewardId rewardId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);
        ArgumentNullException.ThrowIfNull(rewardId);

        // A rejected redemption refunded the points, so it does not count against a cap. Counting it
        // would punish somebody for a failure that was the platform's.
        return await Redemptions
            .AsNoTracking()
            .CountAsync(
                redemption =>
                    redemption.CreatorId == creatorId
                    && redemption.RewardId == rewardId
                    && redemption.State != CreatorRewardState.Rejected,
                cancellationToken);
    }

    public async Task<DateTimeOffset?> LastRedeemedAtAsync(
        CreatorId creatorId, CreatorRewardId rewardId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);
        ArgumentNullException.ThrowIfNull(rewardId);

        var rows = await Redemptions
            .AsNoTracking()
            .Where(redemption =>
                redemption.CreatorId == creatorId
                && redemption.RewardId == rewardId
                && redemption.State != CreatorRewardState.Rejected)
            .OrderByDescending(redemption => redemption.CreatedAt)
            .Select(redemption => redemption.CreatedAt)
            .Take(1)
            .ToListAsync(cancellationToken);

        return rows.Count == 0 ? null : rows[0];
    }

    public async Task<CreatorRewardRedemption?> FindByIdempotencyKeyAsync(
        CreatorId creatorId, string key, CancellationToken cancellationToken = default) =>
        await Redemptions.FirstOrDefaultAsync(
            redemption => redemption.CreatorId == creatorId && redemption.IdempotencyKey == key, cancellationToken);
}
