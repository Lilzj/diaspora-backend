using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Extensibility.Provisioning;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using Microsoft.Extensions.Options;

namespace Diaspora.Modules.Financial.Application.Services;

/// <summary>
/// Opens financial identities and the wallets behind them.
/// <para>
/// <b>The one door into "this owner now has somewhere to keep money".</b> Every path that needs a
/// wallet — a verification approval, an organization being provisioned, a credit grant, a payment —
/// comes through here, so a wallet is never created two different ways with two different sets of
/// wallets behind it.
/// </para>
/// <para>
/// <b>Idempotent throughout.</b> A verification event redelivered a second time finds the account and
/// returns it; a wallet that already exists is returned rather than duplicated. The database's unique
/// indexes are the backstop, and this is the path that means they are rarely reached.
/// </para>
/// <para>
/// <b>It stages and does not save.</b> The caller owns the unit of work, because provisioning is
/// almost always part of something larger.
/// </para>
/// </summary>
public sealed class FinancialIdentityService
{
    private readonly IFinancialAccountRepository _accounts;
    private readonly IWalletRepository _wallets;
    private readonly IWalletProvisioningPolicy _provisioning;
    private readonly IGuidGenerator _guids;
    private readonly IClock _clock;
    private readonly FinancialOptions _options;

    public FinancialIdentityService(
        IFinancialAccountRepository accounts,
        IWalletRepository wallets,
        IWalletProvisioningPolicy provisioning,
        IGuidGenerator guids,
        IClock clock,
        IOptions<FinancialOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _accounts = accounts;
        _wallets = wallets;
        _provisioning = provisioning;
        _guids = guids;
        _clock = clock;
        _options = options.Value;
    }

    /// <summary>The currency an identity is opened in when nothing else says.</summary>
    public string DefaultCurrency =>
        Domain.ValueObjects.Money.NormalizeCurrency(_options.DefaultCurrency) ?? "GBP";

    /// <summary>
    /// Finds or opens an owner's financial identity, with its wallets.
    /// <para>
    /// The account is opened <c>Pending</c> and stays there until something activates it — which for
    /// a member is Trust approving a verification. A pending account holds wallets that refuse
    /// debits, which is exactly right: money can be granted to somebody before they are verified and
    /// cannot leave until they are.
    /// </para>
    /// </summary>
    public async Task<FinancialAccount> GetOrCreateAsync(
        Guid ownerId,
        FinancialAccountType accountType,
        string? currency = null,
        string? displayName = null,
        string? countryCode = null,
        CancellationToken cancellationToken = default)
    {
        var existing = await _accounts.FindByOwnerAsync(ownerId, accountType, cancellationToken);

        if (existing is not null)
        {
            await EnsureWalletsAsync(existing, currency ?? existing.PrimaryCurrency, cancellationToken);

            return existing;
        }

        // ── The country of residence chooses the currency ─────────────────────
        //
        // This read `NormalizeCurrency(currency) ?? DefaultCurrency` and never
        // looked at `countryCode`, which the caller had already supplied — one
        // line below. `UserRegisteredConsumer` passes the member's
        // `CountryOfResidence` and a null currency, so **every member on the
        // platform was opened in GBP**, including members resident in Mauritius
        // pricing their own listings in MUR (UAT-124).
        //
        // Order of precedence: an explicit currency wins, because a caller that
        // named one knows something this does not. Then the country. Then the
        // configured default, for a market the platform has no opinion about.
        var normalized =
            Domain.ValueObjects.Money.NormalizeCurrency(currency)
            ?? Domain.ValueObjects.Money.NormalizeCurrency(CountryCurrency.For(countryCode))
            ?? DefaultCurrency;

        var account = FinancialAccount.Open(
            FinancialAccountId.From(_guids.NewSequentialGuid()),
            ownerId,
            accountType,
            normalized,
            displayName ?? accountType.ToString(),
            _clock.UtcNow,
            countryCode);

        _accounts.Add(account);

        await EnsureWalletsAsync(account, normalized, cancellationToken);

        return account;
    }

    /// <summary>
    /// Opens whatever wallets the provisioning policy says this account should have in a currency.
    /// <para>
    /// Wallets are activated only once the account is active. An active account's new wallet is
    /// active immediately — otherwise adding a currency to an established member would leave them
    /// unable to spend it until somebody noticed.
    /// </para>
    /// </summary>
    public async Task<IReadOnlyList<Wallet>> EnsureWalletsAsync(
        FinancialAccount account, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var normalized = Domain.ValueObjects.Money.NormalizeCurrency(currency) ?? DefaultCurrency;
        var wanted = _provisioning.WalletsFor(account.AccountType, normalized);
        var opened = new List<Wallet>(wanted.Count);

        foreach (var walletType in wanted)
        {
            var existing = await _wallets.FindByAccountAsync(
                account.Id, walletType, normalized, cancellationToken);

            if (existing is not null)
            {
                opened.Add(existing);

                continue;
            }

            var wallet = Wallet.Open(
                WalletId.From(_guids.NewSequentialGuid()),
                account.Id,
                account.OwnerId,
                account.AccountType,
                walletType,
                normalized,
                _clock.UtcNow);

            // **A wallet is open for business from the moment it exists, and the account is what
            // decides where money may go.**
            //
            // This used to activate a wallet only on an Active account, which contradicted every
            // other layer. `FinancialAccount.CanCredit` admits a pending account, the deposit
            // handler says so in as many words — "a pending account may receive money and may not
            // spend it, which is exactly right for a deposit" — and `CreatePaymentIntentCommand`
            // lets a pending account pay. Only the wallet disagreed, and `Wallet.CanCredit` refuses
            // anything that is not Active or Frozen.
            //
            // The effect on the deployment was total: fourteen member accounts, every one Pending,
            // so no member could receive money at all. A creator earning could not be paid — the
            // sweep was refused with "that wallet is closed" — and a card deposit would have been
            // captured by the gateway and then failed to post, taking money without crediting it.
            //
            // What verification gates is moving money *out*: `account.CanDebit` is Active-only and
            // the withdrawal handler refuses on it. That gate is untouched.
            wallet.Activate(_clock.UtcNow);

            _wallets.Add(wallet);
            opened.Add(wallet);
        }

        return opened;
    }

    /// <summary>Activates an account and every wallet on it. What a verification approval does.</summary>
    public async Task ActivateAsync(
        FinancialAccount account, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        account.Activate(_clock.UtcNow);
        _accounts.Update(account);

        foreach (var wallet in await _wallets.ListByAccountAsync(account.Id, cancellationToken))
        {
            if (wallet.Status == WalletStatus.Pending)
            {
                wallet.Activate(_clock.UtcNow);
                _wallets.Update(wallet);
            }
        }
    }

    /// <summary>The member's everyday wallet in a currency, opening one if the account has none.</summary>
    public async Task<Wallet?> PrimaryWalletAsync(
        FinancialAccount account, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(account);

        var normalized = Domain.ValueObjects.Money.NormalizeCurrency(currency) ?? DefaultCurrency;

        var wallet = await _wallets.FindByAccountAsync(
            account.Id, WalletType.Primary, normalized, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        var opened = await EnsureWalletsAsync(account, normalized, cancellationToken);

        return opened.FirstOrDefault(candidate => candidate.WalletType == WalletType.Primary);
    }

    /// <summary>
    /// The everyday wallet on an account already identified by its own id.
    /// <para>
    /// <b>Takes an account id rather than a user id, and that is the security property.</b> A deposit
    /// credits the wallet on the intent's own account — the account the intent was opened against —
    /// so there is no path where a request body names whose balance goes up.
    /// </para>
    /// </summary>
    public async Task<Wallet?> PrimaryWalletAsync(
        FinancialAccountId accountId, string currency, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(accountId);

        var normalized = Domain.ValueObjects.Money.NormalizeCurrency(currency) ?? DefaultCurrency;

        var wallet = await _wallets.FindByAccountAsync(
            accountId, WalletType.Primary, normalized, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        var account = await _accounts.FindAsync(accountId, cancellationToken);

        if (account is null)
        {
            return null;
        }

        var opened = await EnsureWalletsAsync(account, normalized, cancellationToken);

        return opened.FirstOrDefault(candidate => candidate.WalletType == WalletType.Primary);
    }

    /// <summary>
    /// The platform's own wallet of a kind in a currency.
    /// <para>
    /// <b>Opens one if the seeder has not, and that is a deliberate safety net rather than
    /// duplication.</b> The platform account is the counterparty to every transaction; a missing
    /// clearing wallet in a currency somebody has just paid in would fail the payment for a reason
    /// the member could do nothing about.
    /// </para>
    /// </summary>
    public async Task<Wallet?> PlatformWalletAsync(
        WalletType walletType, string currency, CancellationToken cancellationToken = default)
    {
        var normalized = Domain.ValueObjects.Money.NormalizeCurrency(currency) ?? DefaultCurrency;

        var wallet = await _wallets.FindPlatformWalletAsync(walletType, normalized, cancellationToken);

        if (wallet is not null)
        {
            return wallet;
        }

        var platform = await _accounts.FindPlatformAccountAsync(cancellationToken);

        if (platform is null)
        {
            return null;
        }

        var opened = await EnsureWalletsAsync(platform, normalized, cancellationToken);

        return opened.FirstOrDefault(candidate => candidate.WalletType == walletType);
    }
}
