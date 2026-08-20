using Diaspora.Application.Abstractions;
using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Application.Mapping;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Contracts.V1.Responses;
using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.SharedKernel.Results;

namespace Diaspora.Modules.Financial.Application.Operations;

/// <summary>
/// Grants credit and posts the entries that make it a balance.
/// <para>
/// <b>Credit lands in the promotional wallet, never the primary one.</b> "What do we owe people" and
/// "what have we given people" are two numbers, and a platform that credited goodwill into a
/// withdrawable balance has issued currency.
/// </para>
/// <para>
/// The counterparty is the platform's own promotional wallet, so the transaction balances like every
/// other. Granting credit costs the platform something and the ledger says where it came from.
/// </para>
/// </summary>
public sealed class GrantCreditCommandHandler
    : ICommandHandler<GrantCreditCommand, CreditGrantResponse>
{
    private readonly ICreditGrantRepository _grants;
    private readonly IWalletRepository _wallets;
    private readonly ILedgerPostingService _ledger;
    private readonly FinancialIdentityService _identities;
    private readonly FinancialAuthorizationService _authorization;
    private readonly CurrencyCatalog _currencies;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly IUnitOfWork _unitOfWork;

    public GrantCreditCommandHandler(
        ICreditGrantRepository grants,
        IWalletRepository wallets,
        ILedgerPostingService ledger,
        FinancialIdentityService identities,
        FinancialAuthorizationService authorization,
        CurrencyCatalog currencies,
        IGuidGenerator guids,
        IClock clock,
        IUnitOfWork unitOfWork)
    {
        _grants = grants;
        _wallets = wallets;
        _ledger = ledger;
        _identities = identities;
        _authorization = authorization;
        _currencies = currencies;
        _guids = guids;
        _clock = clock;
        _unitOfWork = unitOfWork;
    }

    public async Task<Result<CreditGrantResponse>> Handle(
        GrantCreditCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var facts = await _currencies.FactsAsync(command.Currency, cancellationToken);

        if (!await _currencies.IsSupportedAsync(command.Currency, cancellationToken))
        {
            return Result.Failure<CreditGrantResponse>(Error.Validation(
                FinancialErrorCodes.CurrencyNotSupported,
                $"'{command.Currency}' is not a currency this deployment uses."));
        }

        if (!Enum.TryParse<CreditKind>(command.Kind, true, out var kind))
        {
            return Result.Failure<CreditGrantResponse>(Error.Validation(
                FinancialErrorCodes.AmountInvalid,
                $"'{command.Kind}' is not a kind of credit the platform records."));
        }

        var now = _clock.UtcNow;
        var amount = Money.FromAmount(command.Amount, facts.Code, facts.Exponent);

        var account = await _identities.GetOrCreateAsync(
            command.UserId, FinancialAccountType.Personal, facts.Code,
            cancellationToken: cancellationToken);

        await _identities.EnsureWalletsAsync(account, facts.Code, cancellationToken);

        var wallet = await _wallets.FindByAccountAsync(
            account.Id, WalletType.Promotional, facts.Code, cancellationToken);

        var promotional = await _identities.PlatformWalletAsync(
            WalletType.Promotional, facts.Code, cancellationToken);

        if (wallet is null || promotional is null)
        {
            return Result.Failure<CreditGrantResponse>(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                "There is no promotional wallet to credit. The seeder opens the platform's; "
                + "check that it ran."));
        }

        // ── Found by B15.1's audit, 2026-08-10 ───────────────────────────────────────────────
        //
        // The platform's promotional wallet is opened by the seeder and never funded, and no wallet
        // but clearing may run negative — so on any deployment where nobody had posted money into
        // that pot by hand, this endpoint failed with "insufficient funds" and blamed the platform's
        // own wallet. Nothing caught it: the route is staff-only and the suite holds no staff token,
        // which is the platform's DEBT-031.
        //
        // A pre-funded promotional pot is a real operator practice and is kept: where one has been
        // funded, a grant draws on it and the budget visibly falls. Where it has not, the grant is
        // funded from the clearing wallet, which is the platform's own boundary account and the one
        // account allowed to run negative — a platform that has given away more than it has taken in
        // is exactly what a more-negative clearing balance means.
        var source = promotional.CanDebit(amount) ? promotional : await _identities
            .PlatformWalletAsync(WalletType.Clearing, facts.Code, cancellationToken);

        if (source is null)
        {
            return Result.Failure<CreditGrantResponse>(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                "The platform has no clearing wallet in this currency to fund the grant from."));
        }

        // A pending account's wallets accept credit and refuse debits, which is exactly what a
        // welcome bonus needs: it can arrive before verification and cannot leave before it.
        if (wallet.Status == WalletStatus.Pending)
        {
            wallet.Activate(now);
            _wallets.Update(wallet);
        }

        var grant = CreditGrant.Issue(
            CreditGrantId.From(_guids.NewSequentialGuid()),
            wallet.Id,
            command.UserId,
            wallet.OwnerType,
            kind,
            amount,
            command.Reason,
            now,
            _authorization.UserIdOrNull(),
            command.ExpiresAt);

        _grants.Add(grant);

        var posting = await _ledger.PostAsync(
            new LedgerPostingRequest
            {
                TransactionType = TransactionType.Credit,
                Amount = amount,
                Description = command.Reason,
                Audit = AuditStamp.Create(
                    _authorization.UserIdOrNull() is { } staff
                        ? FinancialActor.Staff(staff)
                        : FinancialActor.System(nameof(GrantCreditCommandHandler)),
                    FinancialSource.Operator,
                    command.Reason),
                Legs =
                [
                    new LedgerPostingLeg
                    {
                        WalletId = source.Id,
                        Direction = LedgerDirection.Debit,
                        Amount = amount,
                        EntryType = LedgerEntryType.Bonus
                    },
                    new LedgerPostingLeg
                    {
                        WalletId = wallet.Id,
                        Direction = LedgerDirection.Credit,
                        Amount = amount,
                        EntryType = LedgerEntryType.Bonus
                    }
                ],
                InitiatedByUserId = _authorization.UserIdOrNull(),
                Reference = $"credit:{grant.Id.Value:N}"
            },
            cancellationToken);

        if (posting.IsFailure)
        {
            return Result.Failure<CreditGrantResponse>(posting.Error);
        }

        grant.AttachTransaction(posting.Value.Transaction.Id);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(FinancialMapper.ToResponse(grant, facts.Symbol));
    }
}
