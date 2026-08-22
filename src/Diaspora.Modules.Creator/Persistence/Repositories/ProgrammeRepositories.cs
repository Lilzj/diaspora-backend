using Diaspora.Modules.Creator.Domain.Activity;
using Diaspora.Modules.Creator.Domain.Challenges;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Milestones;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Domain.Verification;
using Diaspora.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Modules.Creator.Persistence.Repositories;

/// <summary>Reads and writes creator-programme verification applications.</summary>
public sealed class CreatorVerificationRepository : ICreatorVerificationRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorVerificationRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorVerificationApplication> Applications =>
        _dbContext.Set<CreatorVerificationApplication>();

    public void Add(CreatorVerificationApplication application) => Applications.Add(application);

    public void Update(CreatorVerificationApplication application) =>
        _dbContext.StageUpdate(application);

    public async Task<CreatorVerificationApplication?> GetAsync(
        CreatorVerificationApplicationId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Loaded().FirstOrDefaultAsync(
            application => application.Id == id, cancellationToken);
    }

    public async Task<CreatorVerificationApplication?> FindForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        var staged = _dbContext.ChangeTracker
            .Entries<CreatorVerificationApplication>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(application => application.CreatorId == creatorId);

        if (staged is not null)
        {
            return staged;
        }

        return await Loaded().FirstOrDefaultAsync(
            application => application.CreatorId == creatorId, cancellationToken);
    }

    public async Task<CreatorVerificationApplication?> FindByIdempotencyKeyAsync(
        string key, CancellationToken cancellationToken = default) =>
        await Loaded().FirstOrDefaultAsync(
            application => application.IdempotencyKey == key, cancellationToken);

    public async Task<IReadOnlyList<CreatorVerificationApplication>> ListLapsedAsync(
        DateTimeOffset asOf, int take, CancellationToken cancellationToken = default) =>
        await Applications
            .Where(application =>
                application.State == CreatorVerificationState.Approved
                && application.ExpiresAt != null
                && application.ExpiresAt <= asOf)
            .OrderBy(application => application.ExpiresAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    private IQueryable<CreatorVerificationApplication> Loaded() =>
        Applications
            .Include(application => application.Evidence)
            .Include(application => application.Acknowledged)
            .Include(application => application.InformationRequests);
}

/// <summary>Reads the milestone catalogue.</summary>
public sealed class CreatorMilestoneRepository : ICreatorMilestoneRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorMilestoneRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorMilestoneDefinition> Milestones =>
        _dbContext.Set<CreatorMilestoneDefinition>();

    public void Add(CreatorMilestoneDefinition definition) => Milestones.Add(definition);

    public async Task<IReadOnlyList<CreatorMilestoneDefinition>> ListActiveAsync(
        CancellationToken cancellationToken = default) =>
        await Milestones
            .Include(milestone => milestone.Rules)
            .Where(milestone => milestone.IsActive)
            .OrderBy(milestone => milestone.SortOrder)
            .ToListAsync(cancellationToken);

    public async Task<CreatorMilestoneDefinition?> GetAsync(
        CreatorMilestoneId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Milestones
            .Include(milestone => milestone.Rules)
            .FirstOrDefaultAsync(milestone => milestone.Id == id, cancellationToken);
    }

    public async Task<CreatorMilestoneDefinition?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<CreatorMilestoneDefinition>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(milestone => milestone.Key == key);

        if (staged is not null)
        {
            return staged;
        }

        return await Milestones
            .Include(milestone => milestone.Rules)
            .FirstOrDefaultAsync(milestone => milestone.Key == key, cancellationToken);
    }

    /// <summary>
    /// Milestones that count one action.
    /// <para>
    /// Filtered in the database on the owned rule table rather than loaded and sifted in memory —
    /// a consumer that ran on every published guide would otherwise materialise the whole catalogue
    /// per event.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<CreatorMilestoneDefinition>> ListForActionAsync(
        string actionType, CancellationToken cancellationToken = default) =>
        await Milestones
            .Include(milestone => milestone.Rules)
            .Where(milestone =>
                milestone.IsActive
                && milestone.Rules.Any(rule => rule.ActionType == actionType))
            .OrderBy(milestone => milestone.SortOrder)
            .ToListAsync(cancellationToken);
}

/// <summary>Reads and writes one creator's standing against a milestone.</summary>
public sealed class CreatorMilestoneProgressRepository : ICreatorMilestoneProgressRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorMilestoneProgressRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorMilestoneProgress> Progress => _dbContext.Set<CreatorMilestoneProgress>();

    public void Add(CreatorMilestoneProgress progress) => Progress.Add(progress);

    public void Update(CreatorMilestoneProgress progress) => _dbContext.StageUpdate(progress);

    public async Task<CreatorMilestoneProgress?> GetAsync(
        CreatorMilestoneProgressId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Progress
            .Include(row => row.Contributions)
            .FirstOrDefaultAsync(row => row.Id == id, cancellationToken);
    }

    public async Task<CreatorMilestoneProgress?> FindAsync(
        CreatorId creatorId,
        CreatorMilestoneId milestoneId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);
        ArgumentNullException.ThrowIfNull(milestoneId);

        var staged = _dbContext.ChangeTracker
            .Entries<CreatorMilestoneProgress>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(row => row.CreatorId == creatorId && row.MilestoneId == milestoneId);

        if (staged is not null)
        {
            return staged;
        }

        return await Progress
            .Include(row => row.Contributions)
            .FirstOrDefaultAsync(
                row => row.CreatorId == creatorId && row.MilestoneId == milestoneId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<CreatorMilestoneProgress>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        return await Progress
            .Include(row => row.Contributions)
            .Where(row => row.CreatorId == creatorId)
            .ToListAsync(cancellationToken);
    }

    public async Task<CreatorMilestoneProgress?> FindByClaimKeyAsync(
        string key, CancellationToken cancellationToken = default) =>
        await Progress
            .Include(row => row.Contributions)
            .FirstOrDefaultAsync(row => row.ClaimIdempotencyKey == key, cancellationToken);
}

/// <summary>Reads the challenge catalogue.</summary>
public sealed class CreatorChallengeRepository : ICreatorChallengeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorChallengeRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorChallenge> Challenges => _dbContext.Set<CreatorChallenge>();

    public void Add(CreatorChallenge challenge) => Challenges.Add(challenge);

    public async Task<CreatorChallenge?> GetAsync(
        CreatorChallengeId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Loaded().FirstOrDefaultAsync(challenge => challenge.Id == id, cancellationToken);
    }

    public async Task<CreatorChallenge?> FindByKeyAsync(
        string key, CancellationToken cancellationToken = default)
    {
        var staged = _dbContext.ChangeTracker
            .Entries<CreatorChallenge>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(challenge => challenge.Key == key);

        if (staged is not null)
        {
            return staged;
        }

        return await Loaded().FirstOrDefaultAsync(
            challenge => challenge.Key == key, cancellationToken);
    }

    public async Task<IReadOnlyList<CreatorChallenge>> ListPublishedAsync(
        int take, CancellationToken cancellationToken = default) =>
        await Loaded()
            .Where(challenge => challenge.IsPublished)
            .OrderBy(challenge => challenge.EndsAt)
            .Take(take)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CreatorChallenge>> ListForActionAsync(
        string actionType, DateTimeOffset asOf, CancellationToken cancellationToken = default) =>
        await Loaded()
            .Where(challenge =>
                challenge.IsPublished
                && !challenge.IsCancelled
                && challenge.StartsAt <= asOf
                && challenge.EndsAt >= asOf
                && challenge.Tasks.Any(task => task.ActionType == actionType))
            .ToListAsync(cancellationToken);

    private IQueryable<CreatorChallenge> Loaded() =>
        Challenges
            .Include(challenge => challenge.Tasks)
            .Include(challenge => challenge.Rules)
            .Include(challenge => challenge.Countries);
}

/// <summary>Reads and writes challenge enrolments, and ranks them.</summary>
public sealed class CreatorChallengeEnrollmentRepository : ICreatorChallengeEnrollmentRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorChallengeEnrollmentRepository(DiasporaDbContext dbContext) =>
        _dbContext = dbContext;

    private DbSet<CreatorChallengeEnrollment> Enrollments =>
        _dbContext.Set<CreatorChallengeEnrollment>();

    public void Add(CreatorChallengeEnrollment enrollment) => Enrollments.Add(enrollment);

    public void Update(CreatorChallengeEnrollment enrollment) => _dbContext.StageUpdate(enrollment);

    public async Task<CreatorChallengeEnrollment?> GetAsync(
        CreatorChallengeEnrollmentId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return await Enrollments
            .Include(enrollment => enrollment.Tasks)
            .FirstOrDefaultAsync(enrollment => enrollment.Id == id, cancellationToken);
    }

    public async Task<CreatorChallengeEnrollment?> FindAsync(
        CreatorChallengeId challengeId,
        CreatorId creatorId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challengeId);
        ArgumentNullException.ThrowIfNull(creatorId);

        var staged = _dbContext.ChangeTracker
            .Entries<CreatorChallengeEnrollment>()
            .Select(entry => entry.Entity)
            .FirstOrDefault(enrollment =>
                enrollment.ChallengeId == challengeId && enrollment.CreatorId == creatorId);

        if (staged is not null)
        {
            return staged;
        }

        return await Enrollments
            .Include(enrollment => enrollment.Tasks)
            .FirstOrDefaultAsync(
                enrollment =>
                    enrollment.ChallengeId == challengeId && enrollment.CreatorId == creatorId,
                cancellationToken);
    }

    public async Task<IReadOnlyList<CreatorChallengeEnrollment>> ListForCreatorAsync(
        CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        return await Enrollments
            .Include(enrollment => enrollment.Tasks)
            .Where(enrollment => enrollment.CreatorId == creatorId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreatorChallengeEnrollment>> ListForChallengeAsync(
        CreatorChallengeId challengeId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challengeId);

        return await Enrollments
            .Include(enrollment => enrollment.Tasks)
            .Where(enrollment => enrollment.ChallengeId == challengeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CreatorChallengeEnrollment>> ListLeaderboardAsync(
        CreatorChallengeId challengeId, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challengeId);

        return await Enrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.ChallengeId == challengeId && enrollment.Score > 0)
            .OrderByDescending(enrollment => enrollment.Score)
            .ThenBy(enrollment => enrollment.JoinedAt)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Where a score sits, counted rather than paged to.
    /// <para>
    /// One COUNT rather than materialising the board, which is what makes "where am I" answerable on
    /// a challenge with fifty thousand participants.
    /// </para>
    /// </summary>
    public async Task<int> RankOfAsync(
        CreatorChallengeId challengeId, long score, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(challengeId);

        var ahead = await Enrollments
            .AsNoTracking()
            .CountAsync(
                enrollment => enrollment.ChallengeId == challengeId && enrollment.Score > score,
                cancellationToken);

        return ahead + 1;
    }

    public async Task<CreatorChallengeEnrollment?> FindByJoinKeyAsync(
        string key, CancellationToken cancellationToken = default) =>
        await Enrollments
            .Include(enrollment => enrollment.Tasks)
            .FirstOrDefaultAsync(enrollment => enrollment.JoinIdempotencyKey == key, cancellationToken);

    public async Task<CreatorChallengeEnrollment?> FindByClaimKeyAsync(
        string key, CancellationToken cancellationToken = default) =>
        await Enrollments
            .Include(enrollment => enrollment.Tasks)
            .FirstOrDefaultAsync(
                enrollment => enrollment.ClaimIdempotencyKey == key, cancellationToken);
}

/// <summary>
/// Reads and writes the recognised-activity ledger, and computes the leaderboards from it.
/// <para>
/// <b>Every ranking is a grouped sum in the database.</b> A leaderboard that materialised entries to
/// rank twenty creators would fall over on the first busy month, and §36 of the phase brief requires
/// the query translation to be verified rather than assumed — which the scale suite does against
/// real rows.
/// </para>
/// </summary>
public sealed class CreatorActivityRepository : ICreatorActivityRepository
{
    private readonly DiasporaDbContext _dbContext;

    public CreatorActivityRepository(DiasporaDbContext dbContext) => _dbContext = dbContext;

    private DbSet<CreatorActivityEntry> Entries => _dbContext.Set<CreatorActivityEntry>();

    public void Add(CreatorActivityEntry entry) => Entries.Add(entry);

    public async Task<bool> ExistsAsync(
        CreatorId creatorId, string sourceReference, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        var staged = _dbContext.ChangeTracker
            .Entries<CreatorActivityEntry>()
            .Select(entry => entry.Entity)
            .Any(entry =>
                entry.CreatorId == creatorId && entry.SourceReference == sourceReference);

        if (staged)
        {
            return true;
        }

        return await Entries.AnyAsync(
            entry => entry.CreatorId == creatorId && entry.SourceReference == sourceReference,
            cancellationToken);
    }

    public async Task<IReadOnlyList<CreatorActivityEntry>> ListForCreatorAsync(
        CreatorId creatorId,
        DateTimeOffset? before,
        int take,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        var query = Entries.AsNoTracking().Where(entry => entry.CreatorId == creatorId);

        if (before is { } boundary)
        {
            query = query.Where(entry => entry.OccurredAt < boundary);
        }

        // Newest first, because a history opens at the top. `Id` breaks the tie so the cursor is
        // stable: several entries can share an `OccurredAt` when one publish raises more than one
        // recognised action, and an unstable order would drop or repeat rows across pages.
        return await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Take(Math.Clamp(take, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<long> CountForCreatorAsync(
        CreatorId creatorId, DateTimeOffset? since, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        var query = Entries.AsNoTracking().Where(entry => entry.CreatorId == creatorId);

        if (since is { } from)
        {
            query = query.Where(entry => entry.OccurredAt >= from);
        }

        return await query.LongCountAsync(cancellationToken);
    }

    public async Task<long> CountActionAsync(
        CreatorId creatorId,
        string actionType,
        DateTimeOffset? since,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(creatorId);

        var query = Entries
            .AsNoTracking()
            .Where(entry => entry.CreatorId == creatorId && entry.ActionType == actionType);

        if (since is { } from)
        {
            query = query.Where(entry => entry.OccurredAt >= from);
        }

        return await query.LongCountAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LeaderboardRow>> LeaderboardAsync(
        LeaderboardQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rows = await Filtered(query)
            .GroupBy(entry => entry.CreatorId)
            .Select(group => new { CreatorId = group.Key, Score = group.Sum(entry => entry.Weight) })
            .OrderByDescending(row => row.Score)
            .Take(query.Take)
            .ToListAsync(cancellationToken);

        return rows
            .Select((row, index) => new LeaderboardRow(row.CreatorId, row.Score, index + 1))
            .ToList();
    }

    /// <summary>
    /// One creator's own row, whether or not it is on the page.
    /// <para>
    /// The register asks for <c>viewerEntry</c> so that "where am I" never requires downloading the
    /// whole board. The rank is a count of the creators ahead rather than a position in a
    /// materialised list, for the same reason the challenge board's is.
    /// </para>
    /// </summary>
    public async Task<LeaderboardRow?> ViewerRowAsync(
        LeaderboardQuery query, CreatorId creatorId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(creatorId);

        var mine = await Filtered(query)
            .Where(entry => entry.CreatorId == creatorId)
            .Select(entry => entry.Weight)
            .ToListAsync(cancellationToken);

        // No entries inside the window means unranked rather than a zero-score row — a board that
        // listed everybody who has ever registered is not a ranking.
        if (mine.Count == 0)
        {
            return null;
        }

        var score = mine.Sum();

        var ahead = await Filtered(query)
            .GroupBy(entry => entry.CreatorId)
            .Select(group => new { Score = group.Sum(entry => entry.Weight) })
            .CountAsync(row => row.Score > score, cancellationToken);

        return new LeaderboardRow(creatorId, score, ahead + 1);
    }

    /// <summary>
    /// The entries every ranking reads, filtered but not yet grouped.
    /// <para>
    /// <b>The window is a real filter on <c>OccurredAt</c>.</b> The register asks for a period on the
    /// ranking, and a lifetime total wearing a period label is the exact failure CREATOR-GAP-007
    /// records for analytics.
    /// </para>
    /// <para>
    /// <b>The visibility rule is applied by joining to the creator projection</b>, so a creator who
    /// opted out is absent from the ranking rather than filtered off the page — which would leave a
    /// gap in the rank numbers and disclose that somebody was there.
    /// </para>
    /// <para>
    /// <b>Filtered rather than grouped, and the grouping is left to each caller — because EF will
    /// not translate a <c>GroupBy</c> projected into a user-defined record.</b> `new RankedRow(g.Key,
    /// g.Sum(...))` reads better than an anonymous type and compiles; the provider then rewrites the
    /// aggregate as `g.AsQueryable().Sum(...)`, fails to translate the whole expression, and the
    /// route answers 500 the first time anybody asks for a ranking. Found by a test that seeded rows
    /// rather than asserting against an empty table.
    /// </para>
    /// </summary>
    private IQueryable<CreatorActivityEntry> Filtered(LeaderboardQuery query)
    {
        var entries = Entries.AsNoTracking().AsQueryable();

        if (Since(query) is { } since)
        {
            entries = entries.Where(entry => entry.OccurredAt >= since);
        }

        if (!string.IsNullOrWhiteSpace(query.CountryCode))
        {
            var country = query.CountryCode.Trim().ToUpperInvariant();
            entries = entries.Where(entry => entry.CountryCode == country);
        }

        if (query.CommunityId is { } communityId)
        {
            entries = entries.Where(entry => entry.CommunityId == communityId);
        }

        if (query.Scope == LeaderboardScope.TopCreators)
        {
            // "Top creators" ranks the people the programme has verified; "top contributors" ranks
            // everybody. Two different questions, and the register lists both.
            var verified = _dbContext.Set<Domain.Creators.CreatorProfile>()
                .AsNoTracking()
                .Where(creator => creator.IsVerifiedCreator)
                .Select(creator => creator.Id);

            entries = entries.Where(entry => verified.Contains(entry.CreatorId));
        }

        var visible = _dbContext.Set<Domain.Creators.CreatorProfile>()
            .AsNoTracking()
            .Where(creator =>
                creator.IsDiscoverable && !creator.IsSuspended && !creator.IsRestricted)
            .Select(creator => creator.Id);

        return entries.Where(entry => visible.Contains(entry.CreatorId));
    }

    private static DateTimeOffset? Since(LeaderboardQuery query) => query.Period switch
    {
        LeaderboardPeriod.Last7Days => query.AsOf.AddDays(-7),
        LeaderboardPeriod.Last30Days => query.AsOf.AddDays(-30),
        LeaderboardPeriod.Last90Days => query.AsOf.AddDays(-90),
        LeaderboardPeriod.Year => query.AsOf.AddDays(-365),
        _ => null
    };
}
