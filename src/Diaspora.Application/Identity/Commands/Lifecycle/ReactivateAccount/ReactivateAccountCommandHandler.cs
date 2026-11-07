using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Administration;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Administration;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Lifecycle.ReactivateAccount;

/// <summary>Records a proposed reopening for a second member of staff to decide.</summary>
public sealed class ReactivateAccountCommandHandler
    : ICommandHandler<ReactivateAccountCommand, AccountActionProposalResponse>
{
    private readonly AccountActionProposals _proposals;

    public ReactivateAccountCommandHandler(AccountActionProposals proposals)
    {
        _proposals = proposals;
    }

    public Task<Result<AccountActionProposalResponse>> Handle(
        ReactivateAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _proposals.ProposeAsync(new AccountActionDraft
        {
            Kind = AccountActionKind.Reactivate,
            TargetUserId = request.UserId,
            ReasonCode = request.ReasonCode,
            Note = request.Note
        }, cancellationToken);
    }
}
