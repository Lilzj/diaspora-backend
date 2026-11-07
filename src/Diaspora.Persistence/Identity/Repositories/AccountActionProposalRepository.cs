using Diaspora.Domain.Identity.Administration;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.SharedKernel.Identity;
using Microsoft.EntityFrameworkCore;

namespace Diaspora.Persistence.Identity.Repositories;

/// <inheritdoc cref="IAccountActionProposalRepository" />
public sealed class AccountActionProposalRepository : IAccountActionProposalRepository
{
    private readonly DiasporaDbContext _dbContext;

    public AccountActionProposalRepository(DiasporaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    private DbSet<AccountActionProposal> Proposals => _dbContext.Set<AccountActionProposal>();

    public async Task<AccountActionProposal?> GetByIdAsync(
        AccountActionProposalId id, CancellationToken cancellationToken = default) =>
        await Proposals.FirstOrDefaultAsync(proposal => proposal.Id == id, cancellationToken);

    public async Task<IReadOnlyList<AccountActionProposal>> ListAsync(
        AccountActionProposalStatus? status,
        UserId? targetUserId,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var query = Proposals.AsNoTracking();

        if (status is { } wanted)
        {
            query = query.Where(proposal => proposal.Status == wanted);
        }

        if (targetUserId is not null)
        {
            query = query.Where(proposal => proposal.TargetUserId == targetUserId);
        }

        return await query
            .OrderByDescending(proposal => proposal.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> HasPendingAsync(
        UserId targetUserId, AccountActionKind kind, CancellationToken cancellationToken = default) =>
        await Proposals.AnyAsync(
            proposal => proposal.TargetUserId == targetUserId
                && proposal.Kind == kind
                && proposal.Status == AccountActionProposalStatus.Pending,
            cancellationToken);

    public async Task AddAsync(AccountActionProposal proposal, CancellationToken cancellationToken = default) =>
        await Proposals.AddAsync(proposal, cancellationToken);

    public void Update(AccountActionProposal proposal) =>
        TrackedEntityUpdater.MarkModified(_dbContext, Proposals, proposal);
}
