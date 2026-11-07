using Diaspora.Contracts.Identity.V1;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Administration;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.SharedKernel.Identity;

namespace Diaspora.Application.Identity.Administration;

/// <summary>
/// Answers <see cref="IAccountSuspensionRecord"/> from the account and the confirmed suspension
/// proposal that put it there.
/// </summary>
public sealed class AccountSuspensionRecord : IAccountSuspensionRecord
{
    // A member has few proposals; this is enough to reach the latest confirmed suspension.
    private const int ProposalsRead = 20;

    private readonly IUserRepository _users;
    private readonly IAccountActionProposalRepository _proposals;

    public AccountSuspensionRecord(IUserRepository users, IAccountActionProposalRepository proposals)
    {
        _users = users;
        _proposals = proposals;
    }

    public async Task<AccountSuspension?> GetCurrentAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var id = UserId.From(userId);
        var user = await _users.GetByIdAsync(id, cancellationToken);

        if (user is null || user.AccessState != AccessState.Suspended)
        {
            return null;
        }

        var confirmed = await _proposals.ListAsync(
            AccountActionProposalStatus.Confirmed, id, ProposalsRead, cancellationToken);

        var suspension = confirmed.FirstOrDefault(proposal => proposal.Kind == AccountActionKind.Suspend);

        return new AccountSuspension
        {
            UserId = userId,
            Until = user.SuspendedUntil,
            ProposedBy = suspension?.ProposedBy?.Value,
            ConfirmedBy = suspension?.DecidedBy?.Value
        };
    }
}
