using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Identifiers;
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
    private readonly FinancialIdentityService _identities;
    private readonly IClock _clock;

    public RewardQueryHandlers(
        IRewardAccountRepository accounts,
        IRewardConversionRepository conversions,
        RewardConversionPricer pricer,
        FinancialAuthorizationService authorization,
        FinancialIdentityService identities,
        IClock clock)
    {
        _accounts = accounts;
        _conversions = conversions;
        _pricer = pricer;
        _authorization = authorization;
        _identities = identities;
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

        var entries = account.Entries
            .Where(entry => query.Before is not { } before || entry.OccurredAt <= before)
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

        var terms = await _pricer.TermsAsync(
            caller.Value,
            query.Currency ?? _identities.DefaultCurrency,
            query.CountryCode,
            cancellationToken);

        return Result.Success(new RewardConversionTermsResponse
        {
            IsEnabled = terms.IsEnabled,
            DisabledReason = terms.IsEnabled
                ? null
                : FinancialErrorCodes.RewardConversionNotSupported,
            Currency = terms.Currency,
            MinorUnitsPerPoint = terms.MinorUnitsPerPoint,
            MinimumPoints = terms.MinimumPoints,
            MaximumPoints = terms.MaximumPoints,
            DailyMaximumPoints = terms.DailyMaximumPoints,
            DailyRemainingPoints = terms.DailyRemainingPoints,
            PolicyVersion = terms.PolicyVersion
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

        var terms = await _pricer.TermsAsync(
            caller.Value, query.Currency, query.CountryCode, cancellationToken);

        var account = await _accounts.FindByMemberAsync(caller.Value, cancellationToken);
        var quote = RewardConversionPricer.Quote(terms, query.Points, account?.Balance ?? 0);

        // A refusal is rendered rather than thrown: a member asked to convert two points when the
        // minimum is five should be told that, not shown an error.
        return Result.Success(new RewardConversionPreviewResponse
        {
            Points = quote.Points,
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
/// A repeated reference is a success that awarded nothing rather than a failure — an at-least-once
/// bus redelivers, and a consumer that answered 409 to its second delivery would retry for ever.
/// </para>
/// </summary>
public sealed class GrantRewardPointsCommandHandler
    : ICommandHandler<GrantRewardPointsCommand, RewardBalanceResponse>
{
    private readonly IRewardAccountRepository _accounts;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public GrantRewardPointsCommandHandler(
        IRewardAccountRepository accounts,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _accounts = accounts;
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

        var account = await _accounts.FindByMemberAsync(command.UserId, cancellationToken);

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
            _clock.UtcNow);

        if (awarded)
        {
            _accounts.Update(account);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success(FinancialMapperExtensions.ToResponse(account, _clock.UtcNow));
    }
}
