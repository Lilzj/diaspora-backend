using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Verification;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Identity.Repositories;

/// <inheritdoc cref="IStepUpGrantRepository" />
public sealed class StepUpGrantRepository : IStepUpGrantRepository
{
    private readonly DiasporaDbContext _dbContext;

    public StepUpGrantRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<StepUpGrant> Grants => _dbContext.Set<StepUpGrant>();

    public async Task AddAsync(StepUpGrant grant, CancellationToken cancellationToken = default) =>
        await Grants.AddAsync(grant, cancellationToken);

    public async Task<bool> HasLiveGrantAsync(
        UserId userId,
        SessionId sessionId,
        string scope,
        DateTimeOffset asOf,
        CancellationToken cancellationToken = default)
    {
        // Local first, for the same reason OtpChallengeRepository reads it: a grant written earlier
        // in this unit of work has not been persisted yet. That is not hypothetical here — a test
        // that grants and then immediately exercises the guarded command in one scope would
        // otherwise be told the grant does not exist, and would be right about the database and
        // wrong about the platform.
        var pending = Grants.Local.Any(grant =>
            grant.UserId == userId
            && grant.SessionId == sessionId
            && grant.Scope == scope
            && grant.ExpiresAt > asOf);

        return pending || await Grants.AnyAsync(
            grant =>
                grant.UserId == userId
                && grant.SessionId == sessionId
                && grant.Scope == scope
                && grant.ExpiresAt > asOf,
            cancellationToken);
    }
}
