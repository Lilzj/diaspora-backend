using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Administration;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Administration;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Lifecycle.SuspendAccount;

/// <summary>
/// Records a proposed suspension for a second member of staff to decide.
/// <para>
/// The suspension itself — the account moved, its sessions cut, the member told — happens when the
/// proposal is confirmed. Until then the member is untouched.
/// </para>
/// </summary>
public sealed class SuspendAccountCommandHandler
    : ICommandHandler<SuspendAccountCommand, AccountActionProposalResponse>
{
    private readonly AccountActionProposals _proposals;

    public SuspendAccountCommandHandler(AccountActionProposals proposals)
    {
        _proposals = proposals;
    }

    public Task<Result<AccountActionProposalResponse>> Handle(
        SuspendAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _proposals.ProposeAsync(new AccountActionDraft
        {
            Kind = AccountActionKind.Suspend,
            TargetUserId = request.UserId,
            ReasonCode = request.ReasonCode,
            Note = request.Reason,
            Until = request.SuspendedUntil
        }, cancellationToken);
    }
}
