using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Administration;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Administration;
using Diaspora.Domain.Identity.Errors;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Commands.Administration.ProposeRoleGrant;

/// <summary>Records a proposed grant of Admin or SuperAdmin for a second member of staff to decide.</summary>
public sealed class ProposeRoleGrantCommandHandler
    : ICommandHandler<ProposeRoleGrantCommand, AccountActionProposalResponse>
{
    private readonly AccountActionProposals _proposals;

    public ProposeRoleGrantCommandHandler(AccountActionProposals proposals)
    {
        _proposals = proposals;
    }

    public Task<Result<AccountActionProposalResponse>> Handle(
        ProposeRoleGrantCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (RoleGrants.Parse(request.Role) is not { } role)
        {
            return Task.FromResult(Result.Failure<AccountActionProposalResponse>(
                IdentityAdministrationErrors.UnknownRole(request.Role)));
        }

        return _proposals.ProposeAsync(new AccountActionDraft
        {
            Kind = AccountActionKind.GrantRole,
            TargetUserId = request.UserId,
            Note = request.Reason!,
            Role = role
        }, cancellationToken);
    }
}
