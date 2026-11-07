using Diaspora.Application.Identity.Authorization;
using Diaspora.Application.Identity.Mapping;
using Diaspora.Contracts.Identity.V1.Responses;
using Diaspora.Contracts.Moderation.V1;
using Diaspora.Domain.Abstractions;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Administration;
using Diaspora.Domain.Identity.Errors;
using Diaspora.Domain.Identity.Repositories;
using Diaspora.Domain.Identity.Users;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Identity;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Application.Identity.Administration;

/// <summary>What a member of staff proposes to do to an account.</summary>
public sealed record AccountActionDraft
{
    public required AccountActionKind Kind { get; init; }

    public required Guid TargetUserId { get; init; }

    /// <summary>A moderation reason code's name; null only for a role grant.</summary>
    public string? ReasonCode { get; init; }

    public required string Note { get; init; }

    public DateTimeOffset? Until { get; init; }

    public UserRole? Role { get; init; }
}

/// <summary>
/// Account actions that need a second member of staff: proposing them, and deciding them.
/// <para>
/// <b>The owner's rule (7 Nov 2026).</b> Suspending an account, closing one, reopening one and
/// granting Admin or SuperAdmin are proposed by one member of staff and confirmed by another. The
/// one who decides must be a different person, hold the permission the action itself needs, and be
/// above the account in rank — checked again when they decide, because the account and the people
/// may have changed since it was proposed. Confirming carries the action out in the same save as
/// the decision, so a confirmed proposal and an unchanged account cannot both be true.
/// </para>
/// </summary>
public sealed class AccountActionProposals
{
    private readonly IAccountActionProposalRepository _proposals;
    private readonly IUserRepository _users;
    private readonly StaffAuthority _authority;
    private readonly AccountActionExecutor _executor;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AccountActionProposals(
        IAccountActionProposalRepository proposals,
        IUserRepository users,
        StaffAuthority authority,
        AccountActionExecutor executor,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _proposals = proposals;
        _users = users;
        _authority = authority;
        _executor = executor;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <summary>The permission the action itself needs, which whoever decides it must hold too.</summary>
    public static string PermissionFor(AccountActionKind kind) => kind switch
    {
        AccountActionKind.Suspend => IdentityPermissions.SuspendUser,
        AccountActionKind.Disable => IdentityPermissions.DisableUser,
        // Only those who may close an account may reopen one.
        AccountActionKind.Reactivate => IdentityPermissions.DisableUser,
        AccountActionKind.GrantRole => IdentityPermissions.AssignRoles,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown account action.")
    };

    public async Task<Result<AccountActionProposalResponse>> ProposeAsync(
        AccountActionDraft draft, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        var resolved = await _authority.ResolveAsync(UserId.From(draft.TargetUserId), cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<AccountActionProposalResponse>(resolved.Error);
        }

        var (actor, target) = resolved.Value;
        var now = _clock.UtcNow;

        string? reasonCode = null;

        if (draft.Kind != AccountActionKind.GrantRole)
        {
            if (!ModerationReasons.TryParse(draft.ReasonCode, out var code))
            {
                return Result.Failure<AccountActionProposalResponse>(
                    IdentityAdministrationErrors.UnknownReasonCode(draft.ReasonCode));
            }

            reasonCode = code.ToString();
        }

        var allowed = CanBeProposed(draft, actor, target, now);
        if (allowed.IsFailure)
        {
            return Result.Failure<AccountActionProposalResponse>(allowed.Error);
        }

        if (await _proposals.HasPendingAsync(target.Id, draft.Kind, cancellationToken))
        {
            return Result.Failure<AccountActionProposalResponse>(
                IdentityAdministrationErrors.ProposalAlreadyPending);
        }

        var proposal = AccountActionProposal.Propose(
            draft.Kind,
            target.Id,
            actor.Id,
            reasonCode,
            draft.Note,
            draft.Until,
            draft.Role?.Name,
            now);

        await _proposals.AddAsync(proposal, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await DescribeAsync(proposal, cancellationToken));
    }

    /// <summary>A second member of staff agrees, and the action is carried out.</summary>
    public async Task<Result<AccountActionProposalResponse>> ConfirmAsync(
        Guid proposalId, string? note, CancellationToken cancellationToken)
    {
        var decision = await DecidableAsync(proposalId, cancellationToken);
        if (decision.IsFailure)
        {
            return Result.Failure<AccountActionProposalResponse>(decision.Error);
        }

        var (proposal, actors) = decision.Value;
        var now = _clock.UtcNow;

        // The account first: an action the account refuses (it was suspended meanwhile, say) leaves
        // the proposal open for somebody to decline, rather than confirmed with nothing done.
        var carriedOut = await CarryOutAsync(proposal, actors, now, cancellationToken);
        if (carriedOut.IsFailure)
        {
            return Result.Failure<AccountActionProposalResponse>(carriedOut.Error);
        }

        var confirmed = proposal.Confirm(actors.Actor.Id, note, now);
        if (confirmed.IsFailure)
        {
            return Result.Failure<AccountActionProposalResponse>(confirmed.Error);
        }

        _proposals.Update(proposal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await DescribeAsync(proposal, cancellationToken));
    }

    /// <summary>A second member of staff disagrees; nothing is done to the account.</summary>
    public async Task<Result<AccountActionProposalResponse>> DeclineAsync(
        Guid proposalId, string note, CancellationToken cancellationToken)
    {
        var decision = await DecidableAsync(proposalId, cancellationToken);
        if (decision.IsFailure)
        {
            return Result.Failure<AccountActionProposalResponse>(decision.Error);
        }

        var (proposal, actors) = decision.Value;

        var declined = proposal.Decline(actors.Actor.Id, note, _clock.UtcNow);
        if (declined.IsFailure)
        {
            return Result.Failure<AccountActionProposalResponse>(declined.Error);
        }

        _proposals.Update(proposal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await DescribeAsync(proposal, cancellationToken));
    }

    /// <summary>The proposer takes it back before anybody has decided.</summary>
    public async Task<Result<AccountActionProposalResponse>> WithdrawAsync(
        Guid proposalId, string? note, CancellationToken cancellationToken)
    {
        if (_authority.CallerId is not { } callerId)
        {
            return Result.Failure<AccountActionProposalResponse>(IdentityAdministrationErrors.ActorUnknown);
        }

        var proposal = await _proposals.GetByIdAsync(AccountActionProposalId.From(proposalId), cancellationToken);
        if (proposal is null)
        {
            return Result.Failure<AccountActionProposalResponse>(IdentityAdministrationErrors.ProposalNotFound);
        }

        var withdrawn = proposal.Withdraw(callerId, note, _clock.UtcNow);
        if (withdrawn.IsFailure)
        {
            return Result.Failure<AccountActionProposalResponse>(withdrawn.Error);
        }

        _proposals.Update(proposal);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(await DescribeAsync(proposal, cancellationToken));
    }

    public async Task<AccountActionProposalResponse> DescribeAsync(
        AccountActionProposal proposal, CancellationToken cancellationToken) =>
        (await DescribeAsync([proposal], cancellationToken))[0];

    /// <summary>The proposals with the names of the people they concern, read in one query.</summary>
    public async Task<IReadOnlyList<AccountActionProposalResponse>> DescribeAsync(
        IReadOnlyList<AccountActionProposal> proposals, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(proposals);

        if (proposals.Count == 0)
        {
            return [];
        }

        var ids = proposals
            .SelectMany(proposal => new[] { proposal.TargetUserId, proposal.ProposedBy, proposal.DecidedBy })
            .OfType<UserId>()
            .Distinct()
            .ToList();

        var people = (await _users.GetByIdsAsync(ids, cancellationToken))
            .ToDictionary(user => user.Id);

        return [.. proposals.Select(proposal => AccountActionProposalMapper.ToResponse(proposal, people))];
    }

    /// <summary>
    /// The proposal, if the caller may decide it: it is open, they did not propose it, they hold the
    /// action's permission, and they outrank the account.
    /// </summary>
    private async Task<Result<(AccountActionProposal Proposal, StaffActors Actors)>> DecidableAsync(
        Guid proposalId, CancellationToken cancellationToken)
    {
        var proposal = await _proposals.GetByIdAsync(AccountActionProposalId.From(proposalId), cancellationToken);
        if (proposal is null)
        {
            return Result.Failure<(AccountActionProposal, StaffActors)>(IdentityAdministrationErrors.ProposalNotFound);
        }

        if (!proposal.IsPending)
        {
            return Result.Failure<(AccountActionProposal, StaffActors)>(IdentityAdministrationErrors.ProposalNotPending);
        }

        // Before anything else: this is the refusal the rule exists for, and it should be the one
        // the proposer sees.
        if (_authority.CallerId is { } callerId && callerId == proposal.ProposedBy)
        {
            return Result.Failure<(AccountActionProposal, StaffActors)>(IdentityAdministrationErrors.ProposerCannotDecide);
        }

        var permission = PermissionFor(proposal.Kind);
        if (!await _authority.HoldsAsync(permission, cancellationToken))
        {
            return Result.Failure<(AccountActionProposal, StaffActors)>(
                IdentityAdministrationErrors.MissingPermission(permission));
        }

        var resolved = await _authority.ResolveAsync(proposal.TargetUserId, cancellationToken);
        if (resolved.IsFailure)
        {
            return Result.Failure<(AccountActionProposal, StaffActors)>(resolved.Error);
        }

        // Granting SuperAdmin keeps its own rule at both ends: only a SuperAdmin proposes it, and
        // only a SuperAdmin confirms or declines it.
        if (proposal.Kind == AccountActionKind.GrantRole
            && RoleGrants.Parse(proposal.Role) == UserRole.SuperAdmin
            && resolved.Value.Actor.Role != UserRole.SuperAdmin)
        {
            return Result.Failure<(AccountActionProposal, StaffActors)>(IdentityAdministrationErrors.SuperAdminOnly);
        }

        return Result.Success((proposal, resolved.Value));
    }

    private static Result CanBeProposed(
        AccountActionDraft draft, User actor, User target, DateTimeOffset now)
    {
        switch (draft.Kind)
        {
            case AccountActionKind.Suspend:
                if (!target.AccessState.CanTransitionTo(AccessState.Suspended))
                {
                    return Result.Failure(UserErrors.InvalidStateTransition(target.AccessState, AccessState.Suspended));
                }

                return draft.Until is { } until && until <= now
                    ? Result.Failure(IdentityAdministrationErrors.SuspensionEndHasPassed)
                    : Result.Success();

            case AccountActionKind.Disable:
                return target.AccessState.CanTransitionTo(AccessState.Disabled)
                    ? Result.Success()
                    : Result.Failure(UserErrors.InvalidStateTransition(target.AccessState, AccessState.Disabled));

            case AccountActionKind.Reactivate:
                return target.AccessState == AccessState.Disabled && !target.IsAnonymized
                    ? Result.Success()
                    : Result.Failure(UserErrors.InvalidStateTransition(target.AccessState, AccessState.LimitedAccess));

            case AccountActionKind.GrantRole:
                if (draft.Role is not { } role || !RoleGrants.NeedsSecondStaff(role))
                {
                    return Result.Failure(IdentityAdministrationErrors.RoleChangeIsImmediate);
                }

                if (role == UserRole.SuperAdmin && actor.Role != UserRole.SuperAdmin)
                {
                    return Result.Failure(IdentityAdministrationErrors.SuperAdminOnly);
                }

                return target.Role == role
                    ? Result.Failure(IdentityAdministrationErrors.AlreadyHoldsRole)
                    : Result.Success();

            default:
                throw new ArgumentOutOfRangeException(nameof(draft), draft.Kind, "Unknown account action.");
        }
    }

    private async Task<Result> CarryOutAsync(
        AccountActionProposal proposal, StaffActors actors, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var attribution = new StaffAttribution(actors.Actor.Id, proposal.ProposedBy);
        var target = actors.Target;

        switch (proposal.Kind)
        {
            case AccountActionKind.Suspend:
                if (proposal.Until is { } until && until <= now)
                {
                    return Result.Failure(IdentityAdministrationErrors.SuspensionEndHasPassed);
                }

                return await _executor.SuspendAsync(
                    target, proposal.ReasonCode!, proposal.Note, proposal.Until, attribution, now, cancellationToken);

            case AccountActionKind.Disable:
                return await _executor.DisableAsync(
                    target, proposal.ReasonCode!, proposal.Note, attribution, now, cancellationToken);

            case AccountActionKind.Reactivate:
                return await _executor.ReactivateAsync(
                    target, proposal.ReasonCode, proposal.Note, attribution, now, cancellationToken);

            case AccountActionKind.GrantRole:
                if (RoleGrants.Parse(proposal.Role) is not { } role)
                {
                    return Result.Failure(IdentityAdministrationErrors.UnknownRole(proposal.Role));
                }

                if (target.Role == role)
                {
                    return Result.Failure(IdentityAdministrationErrors.AlreadyHoldsRole);
                }

                await _executor.ChangeRoleAsync(target, role, proposal.Note, attribution, now, cancellationToken);
                return Result.Success();

            default:
                throw new ArgumentOutOfRangeException(nameof(proposal), proposal.Kind, "Unknown account action.");
        }
    }
}
