using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Administration;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Administration;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Lifecycle.DisableAccount;

/// <summary>Records a proposed closure for a second member of staff to decide.</summary>
public sealed class DisableAccountCommandHandler
    : ICommandHandler<DisableAccountCommand, AccountActionProposalResponse>
{
    private readonly AccountActionProposals _proposals;

    public DisableAccountCommandHandler(AccountActionProposals proposals)
    {
        _proposals = proposals;
    }

    public Task<Result<AccountActionProposalResponse>> Handle(
        DisableAccountCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return _proposals.ProposeAsync(new AccountActionDraft
        {
            Kind = AccountActionKind.Disable,
            TargetUserId = request.UserId,
            ReasonCode = request.ReasonCode,
            Note = request.Reason
        }, cancellationToken);
    }
}
