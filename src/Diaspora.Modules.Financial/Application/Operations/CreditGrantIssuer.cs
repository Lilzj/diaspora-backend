using Diaspora.Modules.Financial.Application.Services;
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
/// Writes a credit grant and posts the entries that make it a balance. Saves nothing.
/// <para>
/// <b>The one way credit is issued,</b> shared by the direct grant and the approval of a grant request
/// (7 Nov 2026), so the two cannot disagree about where the money comes from. The guards (who, how
/// much, why) are the callers': they differ between the two, and this only moves the money.
/// </para>
/// <para>
/// <b>Credit lands in the promotional wallet, never the primary one.</b> "What do we owe people" and
/// "what have we given people" are two numbers, and a platform that credited goodwill into a
/// withdrawable balance has issued currency.
/// </para>
/// </summary>
public sealed class CreditGrantIssuer
{
    private readonly ICreditGrantRepository _grants;
    private readonly IWalletRepository _wallets;
    private readonly ILedgerPostingService _ledger;
    private readonly FinancialIdentityService _identities;
    private readonly IGuidGenerator _guids;

    public CreditGrantIssuer(
        ICreditGrantRepository grants,
        IWalletRepository wallets,
        ILedgerPostingService ledger,
        FinancialIdentityService identities,
        IGuidGenerator guids)
    {
        _grants = grants;
        _wallets = wallets;
        _ledger = ledger;
        _identities = identities;
        _guids = guids;
    }

    /// <summary>
    /// Issues <paramref name="amount"/> of credit to <paramref name="memberUserId"/>.
    /// </summary>
    /// <param name="grantedByUserId">Whose grant it is: the Admin who granted, or who asked.</param>
    /// <param name="approvedByUserId">The second Admin, when a grant request was approved.</param>
    public async Task<Result<CreditGrant>> IssueAsync(
        Guid memberUserId,
        Money amount,
        CreditKind kind,
        string reason,
        DateTimeOffset? expiresAt,
        Guid? grantedByUserId,
        Guid? approvedByUserId,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(amount);
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);

        var account = await _identities.GetOrCreateAsync(
            memberUserId, FinancialAccountType.Personal, amount.Currency,
            cancellationToken: cancellationToken);

        await _identities.EnsureWalletsAsync(account, amount.Currency, cancellationToken);

        var wallet = await _wallets.FindByAccountAsync(
            account.Id, WalletType.Promotional, amount.Currency, cancellationToken);

        var promotional = await _identities.PlatformWalletAsync(
            WalletType.Promotional, amount.Currency, cancellationToken);

        if (wallet is null || promotional is null)
        {
            return Result.Failure<CreditGrant>(Error.Unexpected(
                FinancialErrorCodes.WalletNotFound,
                "There is no promotional wallet to credit. The seeder opens the platform's; "
                + "check that it ran."));
        }

        // ── Found by B15.1's audit, 2026-08-10 ───────────────────────────────────────────────
        //
        // The platform's promotional wallet is opened by the seeder and never funded, and no wallet
        // but clearing may run negative — so on any deployment where nobody had posted money into
        // that pot by hand, granting failed with "insufficient funds" and blamed the platform's own
        // wallet. A pre-funded promotional pot is a real operator practice and is kept: where one
        // has been funded, a grant draws on it and the budget visibly falls. Where it has not, the
        // grant is funded from the clearing wallet, the platform's own boundary account and the one
        // account allowed to run negative.
        var source = promotional.CanDebit(amount) ? promotional : await _identities
            .PlatformWalletAsync(WalletType.Clearing, amount.Currency, cancellationToken);

        if (source is null)
        {
            return Result.Failure<CreditGrant>(Error.Unexpected(
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
            memberUserId,
            wallet.OwnerType,
            kind,
            amount,
            reason,
            now,
            grantedByUserId,
            expiresAt);

        _grants.Add(grant);

        // The audit stamp names both hands when a second Admin approved: the grant record carries
        // the grantor, and the ledger is where anyone reconstructing a balance looks first.
        var auditReason = approvedByUserId is { } approver
            ? $"{grant.Reason} (approved by {approver})"
            : grant.Reason;

        var posting = await _ledger.PostAsync(
            new LedgerPostingRequest
            {
                TransactionType = TransactionType.Credit,
                Amount = amount,
                Description = grant.Reason,
                Audit = AuditStamp.Create(
                    grantedByUserId is { } staff
                        ? FinancialActor.Staff(staff)
                        : FinancialActor.System(nameof(CreditGrantIssuer)),
                    FinancialSource.Operator,
                    auditReason),
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
                InitiatedByUserId = grantedByUserId,
                Reference = $"credit:{grant.Id.Value:N}"
            },
            cancellationToken);

        if (posting.IsFailure)
        {
            return Result.Failure<CreditGrant>(posting.Error);
        }

        grant.AttachTransaction(posting.Value.Transaction.Id);

        return Result.Success(grant);
    }
}
