using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Identity.Repositories;

/// <inheritdoc cref="IOtpChallengeRepository" />
public sealed class OtpChallengeRepository : IOtpChallengeRepository
{
    private readonly DiasporaDbContext _dbContext;

    public OtpChallengeRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<OtpChallenge> Challenges => _dbContext.Set<OtpChallenge>();

    public async Task<OtpChallenge?> GetByIdAsync(
        OtpChallengeId challengeId,
        CancellationToken cancellationToken = default) =>
        await Challenges.FirstOrDefaultAsync(challenge => challenge.Id == challengeId, cancellationToken);

    public async Task<OtpChallenge?> GetLatestAsync(
        UserId userId,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default)
    {
        // Ordered by issue time rather than filtered on status: an exhausted or superseded
        // challenge must still be returned so the caller can explain precisely why the code
        // was refused instead of reporting "no code was ever sent".
        var tracked = Challenges.Local
            .Where(challenge => challenge.UserId == userId && challenge.Purpose == purpose)
            .OrderByDescending(challenge => challenge.IssuedAt)
            .FirstOrDefault();

        var persisted = await Challenges
            .Where(challenge => challenge.UserId == userId && challenge.Purpose == purpose)
            .OrderByDescending(challenge => challenge.IssuedAt)
            .FirstOrDefaultAsync(cancellationToken);

        // A challenge added earlier in the same unit of work has not been written yet, so
        // the query above cannot see it. Registration issues a code and then reads it back
        // in the same request, which is exactly that case.
        if (tracked is null) return persisted;
        if (persisted is null) return tracked;

        return tracked.IssuedAt >= persisted.IssuedAt ? tracked : persisted;
    }

    public async Task<IReadOnlyList<OtpChallenge>> ListPendingAsync(
        UserId userId,
        OtpPurpose purpose,
        CancellationToken cancellationToken = default) =>
        await Challenges
            .Where(challenge =>
                challenge.UserId == userId &&
                challenge.Purpose == purpose &&
                challenge.Status == OtpChallengeStatus.Pending)
            .ToListAsync(cancellationToken);

    public async Task<int> CountIssuedSinceAsync(
        UserId userId,
        OtpPurpose purpose,
        DateTimeOffset since,
        CancellationToken cancellationToken = default) =>
        await Challenges.CountAsync(
            challenge =>
                challenge.UserId == userId &&
                challenge.Purpose == purpose &&
                challenge.IssuedAt >= since,
            cancellationToken);

    public async Task AddAsync(OtpChallenge challenge, CancellationToken cancellationToken = default) =>
        await Challenges.AddAsync(challenge, cancellationToken);

    public void Update(OtpChallenge challenge) =>
        TrackedEntityUpdater.MarkModified(_dbContext, Challenges, challenge);
}
