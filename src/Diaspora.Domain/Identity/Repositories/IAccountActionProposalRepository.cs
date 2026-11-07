using Diaspora.Domain.Identity.Administration;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Domain.Identity.Repositories;

/// <summary>Account actions proposed by one member of staff and decided by a second.</summary>
public interface IAccountActionProposalRepository
{
    Task<AccountActionProposal?> GetByIdAsync(
        AccountActionProposalId id, CancellationToken cancellationToken = default);

    /// <summary>Newest first; every status when <paramref name="status"/> is null.</summary>
    Task<IReadOnlyList<AccountActionProposal>> ListAsync(
        AccountActionProposalStatus? status,
        UserId? targetUserId,
        int limit,
        CancellationToken cancellationToken = default);

    /// <summary>Whether the account already has an open proposal of this kind.</summary>
    Task<bool> HasPendingAsync(
        UserId targetUserId, AccountActionKind kind, CancellationToken cancellationToken = default);

    Task AddAsync(AccountActionProposal proposal, CancellationToken cancellationToken = default);

    void Update(AccountActionProposal proposal);
}
