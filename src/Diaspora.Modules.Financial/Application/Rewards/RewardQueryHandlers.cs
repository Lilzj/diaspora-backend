using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Rewards;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Rewards;

/// <summary>Every read over a member's points. The one write that creates them is below.</summary>
public sealed class RewardQueryHandlers
    : IQueryHandler<GetMyRewardBalanceQuery, RewardBalanceResponse>,
        IQueryHandler<GetMyRewardHistoryQuery, IReadOnlyList<RewardEntryResponse>>,
        IQueryHandler<GetRewardConversionTermsQuery, RewardConversionTermsResponse>,
        IQueryHandler<PreviewRewardConversionQuery, RewardConversionPreviewResponse>,
        IQueryHandler<GetMyRewardConversionsQuery, IReadOnlyList<RewardConversionResponse>>
{
    private readonly IRewardAccountRepository _accounts;
    private readonly IRewardConversionRepository _conversions;
    private readonly RewardConversionPricer _pricer;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IClock _clock;

    public RewardQueryHandlers(
        IRewardAccountRepository accounts,
        IRewardConversionRepository conversions,
        RewardConversionPricer pricer,
        FinancialAuthorizationService authorization,
        IClock clock)
    {
        _accounts = accounts;
        _conversions = conversions;
        _pricer = pricer;
        _authorization = authorization;
        _clock = clock;
    }

    public async Task<Result<RewardBalanceResponse>> Handle(
        GetMyRewardBalanceQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<RewardBalanceResponse>(caller.Error);
        }

        var account = await _accounts.FindByMemberAsync(caller.Value, cancellationToken);

        // A member who has never earned anything gets a zero rather than a 404. "You have no points"
        // is the answer; "that does not exist" is a different and wrong one.
        return Result.Success(FinancialMapperExtensions.ToResponse(account, _clock.UtcNow));
    }

    public async Task<Result<IReadOnlyList<RewardEntryResponse>>> Handle(
        GetMyRewardHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<RewardEntryResponse>>(caller.Error);
        }

        var account = await _accounts.FindByMemberAsync(caller.Value, cancellationToken);

        if (account is null)
        {
            return Result.Success<IReadOnlyList<RewardEntryResponse>>([]);
        }

        // **The boundary is inclusive and what it already showed is excluded.** Comparing
        // `OccurredAt <= before` on its own returned the previous page's last entry again at the
        // head of every page, and returned an entire page again when all of its entries shared one
        // instant — which is what a batch of awards written in one unit of work looks like. The
        // client reads "a full page" as "there is more", so that case scrolled for ever.
        //
        // **No tie-break in the sort, deliberately.** Excluding by identity is what makes the
        // order within one instant irrelevant: every entry sharing the boundary is either already
        // shown and excluded, or not shown and still to come, whichever order the collection
        // happens to load in. A second sort component here would be code no test can kill — the
        // sibling conversions read orders on its timestamp alone for the same reason.
        var entries = account.Entries
            .Where(entry =>
                (query.Before is not { } before || entry.OccurredAt <= before)
                && !query.ExcludeIds.Contains(entry.EntryId))
            .OrderByDescending(entry => entry.OccurredAt)
            .Take(Math.Clamp(query.Take, 1, Domain.Policies.FinancialLimits.MaxLedgerPageSize));

        return Result.Success<IReadOnlyList<RewardEntryResponse>>(
            [.. entries.Select(FinancialMapperExtensions.ToResponse)]);
    }

    public async Task<Result<RewardConversionTermsResponse>> Handle(
        GetRewardConversionTermsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<RewardConversionTermsResponse>(caller.Error);
        }

        // The pricer falls back to the member's own currency when no rate names one.
        var terms = await _pricer.TermsAsync(caller.Value, query.Currency, cancellationToken);

        // Two columns, not the aggregate: the entry history has nothing to say about standing.
        var points = await _accounts.FindStandingAsync(caller.Value, cancellationToken);

        var standing = RewardConversionPricer.Standing(
            terms, points?.Balance ?? 0, points?.IsConversionBlocked ?? false);

        return Result.Success(new RewardConversionTermsResponse
        {
            IsEnabled = terms.IsEnabled,
            DisabledReason = terms.DisabledCode,
            Currency = terms.Currency,
            Exponent = terms.Exponent,
            Symbol = terms.Symbol,
            MinorUnitsPerPoint = terms.MinorUnitsPerPoint,
            MinimumPoints = terms.MinimumPoints,
            MaximumPoints = terms.MaximumPoints,
            DailyMaximumPoints = terms.DailyMaximumPoints,
            DailyRemainingPoints = terms.DailyRemainingPoints,
            PolicyVersion = terms.PolicyVersion,
            Member = new RewardConversionMemberResponse
            {
                CanConvert = standing.CanConvert,
                ReasonCode = standing.ReasonCode,
                ReasonMessage = standing.ReasonMessage,
                MaximumNow = standing.MaximumNow
            }
        });
    }

    public async Task<Result<RewardConversionPreviewResponse>> Handle(
        PreviewRewardConversionQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<RewardConversionPreviewResponse>(caller.Error);
        }

        var terms = await _pricer.TermsAsync(caller.Value, query.Currency, cancellationToken);

        var account = await _accounts.FindByMemberAsync(caller.Value, cancellationToken);
        var quote = RewardConversionPricer.Quote(
            terms, query.Points, account?.Balance ?? 0,
            ConvertRewardPointsCommandHandler.IsHeld(account));

        // A refusal is rendered rather than thrown: a member asked to convert two points when the
        // minimum is five should be told that, not shown an error.
        return Result.Success(new RewardConversionPreviewResponse
        {
            Points = quote.Points,
            PointsConsumed = quote.PointsConsumed,
            Credit = FinancialMapper.ToResponse(quote.Credit, terms.Symbol),
            MinorUnitsPerPoint = quote.MinorUnitsPerPoint,
            IsPermitted = quote.IsPermitted,
            RefusalCode = quote.RefusalCode,
            RefusalMessage = quote.RefusalMessage,
            PolicyVersion = quote.PolicyVersion
        });
    }

    public async Task<Result<IReadOnlyList<RewardConversionResponse>>> Handle(
        GetMyRewardConversionsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<RewardConversionResponse>>(caller.Error);
        }

        var conversions = await _conversions.ListForMemberAsync(
            caller.Value, query.Before, query.ExcludeIds, query.Take, cancellationToken);

        return Result.Success<IReadOnlyList<RewardConversionResponse>>(
            [.. conversions.Select(conversion => FinancialMapperExtensions.ToResponse(conversion))]);
    }
}

/// <summary>
/// Awards points, once per grant reference.
/// <para>
/// A separate class from the reads above because DSP4001 refuses a query handler that saves — and
/// the rule is right: a type whose transactional behaviour depends on which method was called is a
/// type nobody can reason about from its signature.
/// </para>
/// <para>
/// A repeated reference is a success that awarded nothing rather than a failure — a retried staff
/// request must not answer 409 for a grant that already landed. It is answered before the limits,
/// because the grant it repeats already counts toward the grantor's day.
/// </para>
/// <para>
/// <b>The limits fail closed.</b> Most financial policies read a missing limit as "no limit stated";
/// this one exists only to bound the route, so a <c>reward.grant</c> policy without both limits
/// refuses every grant rather than allowing any.
/// </para>
/// </summary>
public sealed class GrantRewardPointsCommandHandler
    : ICommandHandler<GrantRewardPointsCommand, RewardBalanceResponse>
{
    private readonly IRewardAccountRepository _accounts;
    private readonly FinancialLimitsResolver _limits;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public GrantRewardPointsCommandHandler(
        IRewardAccountRepository accounts,
        FinancialLimitsResolver limits,
        FinancialAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
        _limits = limits;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RewardBalanceResponse>> Handle(
        GrantRewardPointsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.Points <= 0)
        {
            return Result.Failure<RewardBalanceResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid, "A grant of no points is not a grant."));
        }

        var grantor = _authorization.RequireUserId();

        if (grantor.IsFailure)
        {
            return Result.Failure<RewardBalanceResponse>(grantor.Error);
        }

        var account = await _accounts.FindByMemberAsync(command.UserId, cancellationToken);

        if (account is not null && account.HasGranted(command.GrantReference))
        {
            return Result.Success(FinancialMapperExtensions.ToResponse(account, _clock.UtcNow));
        }

        var policy = await _limits.MostSpecificAsync(
            FinancialPolicyKeys.RewardGrant, countryCode: null, cancellationToken);

        var refusal = await RefuseAsync(policy, grantor.Value, command.Points, cancellationToken);

        if (refusal is not null)
        {
            return Result.Failure<RewardBalanceResponse>(refusal);
        }

        if (account is null)
        {
            account = RewardAccount.Open(
                RewardAccountId.From(_guids.NewSequentialGuid()), command.UserId, _clock.UtcNow);

            _accounts.Add(account);
        }

        var awarded = account.Grant(
            _guids.NewSequentialGuid(),
            command.Points,
            command.Source,
            command.GrantReference,
            _clock.UtcNow,
            grantedByUserId: grantor.Value,
            reason: command.Reason,

            // The version of the limits that allowed it. Not refused, so the policy is there.
            policyVersion: policy!.VersionLabel);

        if (awarded)
        {
            _accounts.Update(account);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(FinancialMapperExtensions.ToResponse(account, _clock.UtcNow));
    }

    /// <summary>
    /// Why the <c>reward.grant</c> policy refuses this grant, or null when it allows it.
    /// <para>
    /// The daily ceiling is the grantor's, over a rolling day, and two grants racing from one staff
    /// member can both pass it. That is a bound on one operator's hand, not a ledger invariant;
    /// a second approver above a threshold is the stronger control, and it is still to come.
    /// </para>
    /// </summary>
    private async Task<Error?> RefuseAsync(
        FinancialPolicy? policy, Guid grantorUserId, long points, CancellationToken cancellationToken)
    {
        var maximum = policy?.Int64Parameter(FinancialPolicyParameters.MaximumPoints);
        var dailyMaximum = policy?.Int64Parameter(FinancialPolicyParameters.DailyMaximumPoints);

        if (maximum is not > 0 || dailyMaximum is not > 0)
        {
            return Error.Conflict(
                FinancialErrorCodes.RewardGrantLimitsUnset,
                "Staff grants are off until a reward.grant policy states a per-grant and a daily maximum.");
        }

        if (points > maximum)
        {
            return Error.Conflict(
                FinancialErrorCodes.RewardGrantAboveMaximum,
                $"The largest single grant is {maximum} points.");
        }

        var grantedToday = await _accounts.PointsGrantedBySinceAsync(
            grantorUserId, _clock.UtcNow.AddDays(-1), cancellationToken);

        return grantedToday + points > dailyMaximum
            ? Error.Conflict(
                FinancialErrorCodes.RewardGrantDailyLimitReached,
                $"That would take you past the {dailyMaximum} points one person may grant in a day. "
                + $"You have granted {grantedToday} since this time yesterday.")
            : null;
    }
}

/// <summary>
/// Holds a member's points from conversion, or lifts the hold, and keeps the decision.
/// <para>
/// A hold may come before any earning, so a member with no points account gets one, held. Lifting a
/// hold that is not in force changes nothing and records nothing: there was no decision to make.
/// </para>
/// </summary>
public sealed class SetRewardConversionHoldCommandHandler
    : ICommandHandler<SetRewardConversionHoldCommand, RewardConversionHoldResponse>
{
    private readonly IRewardAccountRepository _accounts;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public SetRewardConversionHoldCommandHandler(
        IRewardAccountRepository accounts,
        FinancialAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RewardConversionHoldResponse>> Handle(
        SetRewardConversionHoldCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var decider = _authorization.RequireUserId();

        if (decider.IsFailure)
        {
            return Result.Failure<RewardConversionHoldResponse>(decider.Error);
        }

        var account = await _accounts.FindByMemberAsync(command.UserId, cancellationToken);
        var now = _clock.UtcNow;

        if (command.IsHeld)
        {
            if (account is null)
            {
                account = RewardAccount.Open(
                    RewardAccountId.From(_guids.NewSequentialGuid()), command.UserId, now);

                _accounts.Add(account);
            }

            account.BlockConversion(_guids.NewSequentialGuid(), command.Reason, decider.Value, now);
        }
        else if (account is null
            || !account.AllowConversion(_guids.NewSequentialGuid(), command.Reason, decider.Value, now))
        {
            return Result.Success(FinancialMapperExtensions.ToHoldResponse(command.UserId, account));
        }

        _accounts.Update(account);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToHoldResponse(command.UserId, account));
    }
}
