using Diaspora.Application.Abstractions;
using Diaspora.Application.Identity.Administration;
using Diaspora.Application.Identity.Authorization;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Domain.Identity.Administration;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Queries.Administration.AccountActions;

/// <summary>
/// Proposed account actions, newest first: the queue a second member of staff works from
/// (7 Nov 2026). Filtered by status (<c>Pending</c> for the queue) and by account.
/// </summary>
[RequirePermission(IdentityPermissions.ReadAnyUser)]
public sealed record ListAccountActionsQuery : IQuery<IReadOnlyList<AccountActionProposalResponse>>
{
    /// <summary><c>Pending</c>, <c>Confirmed</c>, <c>Declined</c> or <c>Withdrawn</c>; all when null.</summary>
    public string? Status { get; init; }

    /// <summary>Only proposals about this account, when set.</summary>
    public Guid? TargetUserId { get; init; }

    public int Limit { get; init; } = 50;
}

/// <summary>One proposed account action.</summary>
[RequirePermission(IdentityPermissions.ReadAnyUser)]
public sealed record GetAccountActionQuery : IQuery<AccountActionProposalResponse>
{
    public required Guid ProposalId { get; init; }
}

public sealed class ListAccountActionsQueryHandler
    : IQueryHandler<ListAccountActionsQuery, IReadOnlyList<AccountActionProposalResponse>>
{
    private const int MaxLimit = 200;

    private readonly IAccountActionProposalRepository _repository;
    private readonly AccountActionProposals _proposals;

    public ListAccountActionsQueryHandler(
        IAccountActionProposalRepository repository, AccountActionProposals proposals)
    {
        _repository = repository;
        _proposals = proposals;
    }

    public async Task<Result<IReadOnlyList<AccountActionProposalResponse>>> Handle(
        ListAccountActionsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        AccountActionProposalStatus? status = null;

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            if (!Enum.TryParse<AccountActionProposalStatus>(request.Status.Trim(), ignoreCase: true, out var parsed)
                || !Enum.IsDefined(parsed))
            {
                return Result.Failure<IReadOnlyList<AccountActionProposalResponse>>(Error.Validation(
                    "Identity.Administration.UnknownProposalStatus",
                    $"'{request.Status}' is not a proposal status. Use Pending, Confirmed, Declined or Withdrawn."));
            }

            status = parsed;
        }

        var found = await _repository.ListAsync(
            status,
            request.TargetUserId is { } target ? UserId.From(target) : null,
            Math.Clamp(request.Limit, 1, MaxLimit),
            cancellationToken);

        return Result.Success(await _proposals.DescribeAsync(found, cancellationToken));
    }
}

public sealed class GetAccountActionQueryHandler
    : IQueryHandler<GetAccountActionQuery, AccountActionProposalResponse>
{
    private readonly IAccountActionProposalRepository _repository;
    private readonly AccountActionProposals _proposals;

    public GetAccountActionQueryHandler(
        IAccountActionProposalRepository repository, AccountActionProposals proposals)
    {
        _repository = repository;
        _proposals = proposals;
    }

    public async Task<Result<AccountActionProposalResponse>> Handle(
        GetAccountActionQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var proposal = await _repository.GetByIdAsync(
            AccountActionProposalId.From(request.ProposalId), cancellationToken);

        return proposal is null
            ? Result.Failure<AccountActionProposalResponse>(IdentityAdministrationErrors.ProposalNotFound)
            : Result.Success(await _proposals.DescribeAsync(proposal, cancellationToken));
    }
}
