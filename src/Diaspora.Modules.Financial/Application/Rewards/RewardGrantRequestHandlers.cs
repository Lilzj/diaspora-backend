using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Rewards;

/// <summary>
/// Asks for a staff grant above the approval threshold (RWD-10). Nothing is granted until a second
/// Admin approves.
/// </summary>
public sealed class RequestRewardGrantCommandHandler
    : ICommandHandler<RequestRewardGrantCommand, RewardGrantRequestResponse>
{
    private readonly IRewardGrantRequestRepository _requests;
    private readonly IRewardAccountRepository _accounts;
    private readonly RewardGrantLimits _limits;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public RequestRewardGrantCommandHandler(
        IRewardGrantRequestRepository requests,
        IRewardAccountRepository accounts,
        RewardGrantLimits limits,
        FinancialAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _accounts = accounts;
        _limits = limits;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RewardGrantRequestResponse>> Handle(
        RequestRewardGrantCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var asker = _authorization.RequireUserId();

        if (asker.IsFailure)
        {
            return Result.Failure<RewardGrantRequestResponse>(asker.Error);
        }

        if (RewardGrantLimits.RefuseSelf(asker.Value, command.UserId) is { } self)
        {
            return Result.Failure<RewardGrantRequestResponse>(self);
        }

        // A retry replays the request it already made, whoever's day it is now.
        if (await _requests.FindByReferenceAsync(command.GrantReference, cancellationToken) is { } existing)
        {
            return Result.Success(FinancialMapperExtensions.ToResponse(existing));
        }

        var account = await _accounts.FindByMemberAsync(command.UserId, cancellationToken);

        if (account is not null && account.HasGranted(command.GrantReference))
        {
            return Result.Failure<RewardGrantRequestResponse>(Error.Conflict(
                FinancialErrorCodes.RewardGrantDuplicate,
                "These points were already granted under that reference."));
        }

        var verdict = await _limits.CheckAsync(asker.Value, command.Points, cancellationToken);

        if (verdict.Refusal is not null)
        {
            return Result.Failure<RewardGrantRequestResponse>(verdict.Refusal);
        }

        var request = RewardGrantRequest.Raise(
            RewardGrantRequestId.From(_guids.NewSequentialGuid()),
            command.UserId,
            command.Points,
            command.Source,
            command.GrantReference,
            command.Reason,
            asker.Value,
            _clock.UtcNow);

        _requests.Add(request);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(request));
    }
}

/// <summary>
/// Approves, rejects or withdraws a staff grant request (RWD-10).
/// <para>
/// <b>An approval grants the points here, in this transaction,</b> through the same
/// <see cref="RewardAccount.Grant"/> as any staff grant: the asker as grantor, the approver beside
/// them, the asker's reason, the policy version that allowed it, and the same 7-day vesting. The
/// limits are checked again against the <em>asker's</em> day, because the grant counts toward it from
/// the moment it lands.
/// </para>
/// </summary>
public sealed class DecideRewardGrantRequestCommandHandler
    : ICommandHandler<DecideRewardGrantRequestCommand, RewardGrantRequestResponse>
{
    private readonly IRewardGrantRequestRepository _requests;
    private readonly IRewardAccountRepository _accounts;
    private readonly RewardGrantLimits _limits;
    private readonly RewardVesting _vesting;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public DecideRewardGrantRequestCommandHandler(
        IRewardGrantRequestRepository requests,
        IRewardAccountRepository accounts,
        RewardGrantLimits limits,
        RewardVesting vesting,
        FinancialAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _requests = requests;
        _accounts = accounts;
        _limits = limits;
        _vesting = vesting;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RewardGrantRequestResponse>> Handle(
        DecideRewardGrantRequestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var decider = _authorization.RequireUserId();

        if (decider.IsFailure)
        {
            return Result.Failure<RewardGrantRequestResponse>(decider.Error);
        }

        var request = await _requests.FindAsync(
            RewardGrantRequestId.From(command.RequestId), cancellationToken);

        if (request is null)
        {
            return Result.Failure<RewardGrantRequestResponse>(Error.NotFound(
                FinancialErrorCodes.RewardGrantRequestNotFound, "No grant request has that id."));
        }

        if (!request.IsPending)
        {
            return Result.Failure<RewardGrantRequestResponse>(Error.Conflict(
                FinancialErrorCodes.RewardGrantRequestDecided,
                $"That grant request is already {request.Status.ToString().ToLowerInvariant()}."));
        }

        var now = _clock.UtcNow;
        var asked = request.RequestedByUserId == decider.Value;

        if (!command.Approve)
        {
            if (asked)
            {
                request.Withdraw(command.Note!, now);
            }
            else
            {
                request.Reject(decider.Value, command.Note!, now);
            }

            _requests.Update(request);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(FinancialMapperExtensions.ToResponse(request));
        }

        if (asked)
        {
            return Result.Failure<RewardGrantRequestResponse>(Error.Forbidden(
                FinancialErrorCodes.RewardGrantSelfApproval,
                "Another Admin has to approve a grant you asked for. You can withdraw it."));
        }

        if (RewardGrantLimits.RefuseSelf(decider.Value, request.MemberUserId) is { } self)
        {
            return Result.Failure<RewardGrantRequestResponse>(self);
        }

        var verdict = await _limits.CheckAsync(request.RequestedByUserId, request.Points, cancellationToken);

        if (verdict.Refusal is not null)
        {
            return Result.Failure<RewardGrantRequestResponse>(verdict.Refusal);
        }

        var account = await _accounts.FindByMemberAsync(request.MemberUserId, cancellationToken);

        if (account is null)
        {
            account = RewardAccount.Open(
                RewardAccountId.From(_guids.NewSequentialGuid()), request.MemberUserId, now);

            _accounts.Add(account);
        }

        // Not refused, so the policy is there.
        var policyVersion = verdict.Policy!.VersionLabel;

        var granted = account.Grant(
            _guids.NewSequentialGuid(),
            request.Points,
            request.Source,
            request.GrantReference,
            now,
            grantedByUserId: request.RequestedByUserId,
            reason: request.Reason,
            policyVersion: policyVersion,
            vestsAt: await _vesting.VestsAtAsync(request.MemberUserId, now, cancellationToken),
            approvedByUserId: decider.Value);

        if (granted)
        {
            _accounts.Update(account);
        }

        request.Approve(decider.Value, command.Note, policyVersion, now);
        _requests.Update(request);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(request));
    }
}

/// <summary>
/// The grant request queue. A separate class from the writes above because DSP4001 refuses a query
/// handler that saves.
/// </summary>
public sealed class GetRewardGrantRequestsQueryHandler
    : IQueryHandler<GetRewardGrantRequestsQuery, IReadOnlyList<RewardGrantRequestResponse>>
{
    private readonly IRewardGrantRequestRepository _requests;

    public GetRewardGrantRequestsQueryHandler(IRewardGrantRequestRepository requests) =>
        _requests = requests;

    public async Task<Result<IReadOnlyList<RewardGrantRequestResponse>>> Handle(
        GetRewardGrantRequestsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        RewardGrantRequestStatus? status;

        if (string.IsNullOrWhiteSpace(query.State))
        {
            status = RewardGrantRequestStatus.Pending;
        }
        else if (string.Equals(query.State.Trim(), "all", StringComparison.OrdinalIgnoreCase))
        {
            status = null;
        }
        else if (Enum.TryParse<RewardGrantRequestStatus>(query.State.Trim(), ignoreCase: true, out var parsed)
            && Enum.IsDefined(parsed))
        {
            status = parsed;
        }
        else
        {
            return Result.Failure<IReadOnlyList<RewardGrantRequestResponse>>(Error.Validation(
                FinancialErrorCodes.RewardGrantRequestStateInvalid,
                "State is Pending, Approved, Rejected, Withdrawn or all."));
        }

        var requests = await _requests.ListAsync(
            status, query.Before, query.ExcludeIds, query.Take, cancellationToken);

        return Result.Success<IReadOnlyList<RewardGrantRequestResponse>>(
            [.. requests.Select(FinancialMapperExtensions.ToResponse)]);
    }
}
