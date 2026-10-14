using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Policies;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using Diaspora.Modules.Financial.Extensibility.Settlement;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Withdrawals;

/// <summary>The caller's payout requests, cursor-paged.</summary>
public sealed class GetMyWithdrawalsQueryHandler
    : IQueryHandler<GetMyWithdrawalsQuery, IReadOnlyList<WithdrawalResponse>>
{
    private readonly IWithdrawalRepository _withdrawals;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetMyWithdrawalsQueryHandler(
        IWithdrawalRepository withdrawals,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _withdrawals = withdrawals;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<IReadOnlyList<WithdrawalResponse>>> Handle(
        GetMyWithdrawalsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<WithdrawalResponse>>(caller.Error);
        }

        var status = Enum.TryParse<WithdrawalStatus>(query.Status, true, out var wanted)
            ? wanted
            : (WithdrawalStatus?)null;

        var withdrawals = await _withdrawals.ListForUserAsync(
            caller.Value, status, query.Before, query.ExcludeIds, query.Take, cancellationToken);

        var symbols = await _currencies.AllAsync(cancellationToken);

        return Result.Success<IReadOnlyList<WithdrawalResponse>>(
        [
            .. withdrawals.Select(withdrawal => FinancialMapperExtensions.ToResponse(
                withdrawal,
                symbols.TryGetValue(withdrawal.Currency, out var facts) ? facts.Symbol : null))
        ]);
    }
}

/// <summary>
/// One payout request.
/// <para>
/// Answers 404 rather than 403 on somebody else's, so an identifier cannot be used to discover that a
/// withdrawal exists — the same rule the wallet statement route already follows.
/// </para>
/// </summary>
public sealed class GetWithdrawalQueryHandler : IQueryHandler<GetWithdrawalQuery, WithdrawalResponse>
{
    private readonly IWithdrawalRepository _withdrawals;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;

    public GetWithdrawalQueryHandler(
        IWithdrawalRepository withdrawals,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies)
    {
        _withdrawals = withdrawals;
        _authorization = authorization;
        _currencies = currencies;
    }

    public async Task<Result<WithdrawalResponse>> Handle(
        GetWithdrawalQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<WithdrawalResponse>(caller.Error);
        }

        var withdrawal = await _withdrawals.FindAsync(
            WithdrawalId.From(query.WithdrawalId), cancellationToken);

        if (withdrawal is null
            || (withdrawal.RequestedByUserId != caller.Value
                && !_authorization.Has(FinancialPermissions.WalletReadAny)))
        {
            return Result.Failure<WithdrawalResponse>(Error.NotFound(
                FinancialErrorCodes.WithdrawalNotFound, "That withdrawal does not exist."));
        }

        var symbol = await _currencies.SymbolAsync(withdrawal.Currency, cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(withdrawal, symbol));
    }
}

/// <summary>What the caller may withdraw, what it would cost, and what they hold.</summary>
public sealed class GetWithdrawalLimitsQueryHandler
    : IQueryHandler<GetWithdrawalLimitsQuery, MovementLimitsResponse>
{
    private readonly IWithdrawalRepository _withdrawals;
    private readonly FinancialAuthorizationService _authorization;
    private readonly FinancialLimitsResolver _limits;
    private readonly FinancialIdentityService _identities;
    private readonly MemberCurrency _memberCurrency;
    private readonly CurrencyCatalog _currencies;
    private readonly IEnumerable<ISettlementProvider> _settlement;
    private readonly IClock _clock;

    public GetWithdrawalLimitsQueryHandler(
        IWithdrawalRepository withdrawals,
        FinancialAuthorizationService authorization,
        FinancialLimitsResolver limits,
        FinancialIdentityService identities,
        MemberCurrency memberCurrency,
        CurrencyCatalog currencies,
        IEnumerable<ISettlementProvider> settlement,
        IClock clock)
    {
        _withdrawals = withdrawals;
        _authorization = authorization;
        _limits = limits;
        _identities = identities;
        _memberCurrency = memberCurrency;
        _currencies = currencies;
        _settlement = settlement;
        _clock = clock;
    }

    public async Task<Result<MovementLimitsResponse>> Handle(
        GetWithdrawalLimitsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<MovementLimitsResponse>(caller.Error);
        }

        // The caller's currency — the money they hold, else where they live — unless they asked
        // about another. The platform default is only the last resort.
        var currency = query.Currency
            ?? await _memberCurrency.ForMemberAsync(caller.Value, cancellationToken);
        var facts = await _currencies.FactsAsync(currency, cancellationToken);

        var limits = await _limits.ResolveAsync(
            FinancialPolicyKeys.WithdrawalLimits,
            facts.Code,
            query.CountryCode,
            facts.Exponent,
            cancellationToken);

        var usedToday = await _withdrawals.SumSinceAsync(
            caller.Value, facts.Code, _clock.UtcNow.AddDays(-1), cancellationToken);

        var account = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            facts.Code,
            cancellationToken: cancellationToken);

        var wallet = await _identities.PrimaryWalletAsync(account.Id, facts.Code, cancellationToken);

        var enabled = _settlement.Any();

        return Result.Success(FinancialMapperExtensions.ToResponse(
            limits,
            enabled,
            enabled ? null : FinancialErrorCodes.PayoutNotSupported,
            usedToday,

            // What is actually spendable, so a client never has to derive it from a balance that
            // includes reserved funds a previous withdrawal is already holding.
            wallet?.Available,
            facts.Symbol));
    }
}

/// <summary>Registers, lists, verifies and removes payout destinations.</summary>
public sealed class PayoutDestinationCommandHandlers
    : ICommandHandler<RegisterPayoutDestinationCommand, PayoutDestinationResponse>,
        ICommandHandler<RemovePayoutDestinationCommand, PayoutDestinationResponse>,
        ICommandHandler<VerifyPayoutDestinationCommand, PayoutDestinationResponse>
{
    private readonly IPayoutDestinationRepository _destinations;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public PayoutDestinationCommandHandlers(
        IPayoutDestinationRepository destinations,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _destinations = destinations;
        _identities = identities;
        _authorization = authorization;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<PayoutDestinationResponse>> Handle(
        RegisterPayoutDestinationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PayoutDestinationResponse>(caller.Error);
        }

        if (!Enum.TryParse<PayoutDestinationKind>(command.Kind, true, out var kind))
        {
            return Result.Failure<PayoutDestinationResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid,
                $"'{command.Kind}' is not a payout destination kind the platform recognises."));
        }

        var account = await _identities.GetOrCreateAsync(
            caller.Value,
            FinancialAccountType.Personal,
            command.Currency,
            countryCode: command.CountryCode,
            cancellationToken: cancellationToken);

        PayoutDestination destination;

        try
        {
            destination = PayoutDestination.Register(
                PayoutDestinationId.From(_guids.NewSequentialGuid()),
                caller.Value,
                account.Id,
                kind,
                command.Label,
                command.Currency,
                _clock.UtcNow,
                command.Provider,
                command.ProviderToken,
                command.CountryCode);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PayoutDestinationResponse>(
                Error.Validation(exception.Code, exception.Message));
        }

        _destinations.Add(destination);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(destination));
    }

    public async Task<Result<PayoutDestinationResponse>> Handle(
        RemovePayoutDestinationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<PayoutDestinationResponse>(caller.Error);
        }

        var destination = await _destinations.FindAsync(
            PayoutDestinationId.From(command.DestinationId), cancellationToken);

        if (destination is null || destination.OwnerUserId != caller.Value)
        {
            return Result.Failure<PayoutDestinationResponse>(Error.NotFound(
                FinancialErrorCodes.DestinationNotFound, "That payout destination does not exist."));
        }

        destination.Remove(_clock.UtcNow);
        _destinations.Update(destination);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(destination));
    }

    public async Task<Result<PayoutDestinationResponse>> Handle(
        VerifyPayoutDestinationCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var destination = await _destinations.FindAsync(
            PayoutDestinationId.From(command.DestinationId), cancellationToken);

        if (destination is null)
        {
            return Result.Failure<PayoutDestinationResponse>(Error.NotFound(
                FinancialErrorCodes.DestinationNotFound, "That payout destination does not exist."));
        }

        try
        {
            destination.Verify(_clock.UtcNow, command.Provider, command.ProviderToken);
        }
        catch (DomainException exception)
        {
            return Result.Failure<PayoutDestinationResponse>(
                Error.Conflict(exception.Code, exception.Message));
        }

        _destinations.Update(destination);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapperExtensions.ToResponse(destination));
    }

}

/// <summary>
/// The caller's payout destinations.
/// <para>
/// A separate class from the writes above rather than a fifth method on it: DSP4001 refuses a query
/// handler that saves, and the rule is right — a read and a write in one type is a type whose
/// transactional behaviour depends on which method was called.
/// </para>
/// </summary>
public sealed class GetMyPayoutDestinationsQueryHandler
    : IQueryHandler<GetMyPayoutDestinationsQuery, IReadOnlyList<PayoutDestinationResponse>>
{
    private readonly IPayoutDestinationRepository _destinations;
    private readonly FinancialAuthorizationService _authorization;

    public GetMyPayoutDestinationsQueryHandler(
        IPayoutDestinationRepository destinations, FinancialAuthorizationService authorization)
    {
        _destinations = destinations;
        _authorization = authorization;
    }

    public async Task<Result<IReadOnlyList<PayoutDestinationResponse>>> Handle(
        GetMyPayoutDestinationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var caller = _authorization.RequireUserId();

        if (caller.IsFailure)
        {
            return Result.Failure<IReadOnlyList<PayoutDestinationResponse>>(caller.Error);
        }

        var destinations = await _destinations.ListForUserAsync(
            caller.Value, includeRemoved: false, cancellationToken);

        return Result.Success<IReadOnlyList<PayoutDestinationResponse>>(
            [.. destinations.Select(FinancialMapperExtensions.ToResponse)]);
    }
}
