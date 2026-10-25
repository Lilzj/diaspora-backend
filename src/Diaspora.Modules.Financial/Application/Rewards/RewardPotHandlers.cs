using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Currencies;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Rewards;

/// <summary>
/// Moves money from the platform's Revenue into a currency's rewards pot.
/// <para>
/// <b>Bounded by Revenue, which cannot go negative.</b> A top-up larger than what the platform has
/// earned in that currency is refused before anything is posted, and the posting service would
/// refuse it again if two top-ups raced for the same revenue.
/// </para>
/// </summary>
public sealed class FundRewardsPotCommandHandler
    : ICommandHandler<FundRewardsPotCommand, RewardPotResponse>
{
    private readonly FinancialIdentityService _identities;
    private readonly ILedgerPostingService _ledger;
    private readonly CurrencyCatalog _currencies;
    private readonly FinancialAuthorizationService _authorization;
    private readonly IGuidGenerator _guids;
    private readonly IUnitOfWork _unitOfWork;

    public FundRewardsPotCommandHandler(
        FinancialIdentityService identities,
        ILedgerPostingService ledger,
        CurrencyCatalog currencies,
        FinancialAuthorizationService authorization,
        IGuidGenerator guids,
        IUnitOfWork unitOfWork)
    {
        _identities = identities;
        _ledger = ledger;
        _currencies = currencies;
        _authorization = authorization;
        _guids = guids;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<RewardPotResponse>> Handle(
        FundRewardsPotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var staff = _authorization.RequireUserId();

        if (staff.IsFailure)
        {
            return Result.Failure<RewardPotResponse>(staff.Error);
        }

        if (!await _currencies.IsSupportedAsync(command.Currency, cancellationToken))
        {
            return Result.Failure<RewardPotResponse>(Error.Validation(
                FinancialErrorCodes.CurrencyNotSupported,
                $"'{command.Currency}' is not a currency this deployment uses."));
        }

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);
        var amount = Money.FromAmount(command.Amount, facts.Code, facts.Exponent);

        var revenue = await _identities.PlatformWalletAsync(
            WalletType.Revenue, facts.Code, cancellationToken);
        var pot = await _identities.PlatformWalletAsync(
            WalletType.Rewards, facts.Code, cancellationToken);

        if (revenue is null || pot is null)
        {
            return Result.Failure<RewardPotResponse>(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                $"The platform has no revenue or rewards wallet in {facts.Code}."));
        }

        if (!revenue.CanDebit(amount))
        {
            return Result.Failure<RewardPotResponse>(Error.Conflict(
                FinancialErrorCodes.InsufficientFunds,
                $"Revenue holds less than this top-up in {facts.Code}. The pot can only be funded "
                + "from what the platform has earned."));
        }

        var posting = await _ledger.PostAsync(
            new LedgerPostingRequest
            {
                TransactionType = TransactionType.InternalTransfer,
                Amount = amount,
                Description = command.Reason,
                Audit = AuditStamp.Create(
                    FinancialActor.Staff(staff.Value), FinancialSource.Operator, command.Reason),
                Legs =
                [
                    new LedgerPostingLeg
                    {
                        WalletId = revenue.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = amount,
                        EntryType = LedgerEntryType.InternalTransfer
                    },
                    new LedgerPostingLeg
                    {
                        WalletId = pot.Id,
                        Direction = LedgerDirection.Credit,
                        Amount = amount,
                        EntryType = LedgerEntryType.InternalTransfer
                    }
                ],
                InitiatedByUserId = staff.Value,
                Reference = $"reward-pot-funding:{_guids.NewSequentialGuid():N}"
            },
            cancellationToken);

        if (posting.IsFailure)
        {
            return Result.Failure<RewardPotResponse>(posting.Error);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(RewardPotMapping.ToResponse(pot, facts.Symbol));
    }
}

/// <summary>Every currency's rewards pot, for staff deciding the next top-up.</summary>
public sealed class GetRewardPotsQueryHandler
    : IQueryHandler<GetRewardPotsQuery, IReadOnlyList<RewardPotResponse>>
{
    private readonly ICurrencyRepository _currencies;
    private readonly IWalletRepository _wallets;

    public GetRewardPotsQueryHandler(ICurrencyRepository currencies, IWalletRepository wallets)
    {
        _currencies = currencies;
        _wallets = wallets;
    }

    public async Task<Result<IReadOnlyList<RewardPotResponse>>> Handle(
        GetRewardPotsQuery query, CancellationToken cancellationToken)
    {
        var pots = new List<RewardPotResponse>();

        foreach (var currency in await _currencies.ListActiveAsync(cancellationToken))
        {
            var pot = await _wallets.FindPlatformWalletAsync(
                WalletType.Rewards, currency.Code, cancellationToken);

            if (pot is not null)
            {
                pots.Add(RewardPotMapping.ToResponse(pot, currency.Symbol));
            }
        }

        return Result.Success<IReadOnlyList<RewardPotResponse>>(pots);
    }
}

internal static class RewardPotMapping
{
    public static RewardPotResponse ToResponse(Wallet pot, string? symbol) =>
        new()
        {
            Currency = pot.Currency,
            Available = FinancialMapper.ToResponse(pot.Available, symbol)
        };
}
