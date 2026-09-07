using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Application.Services;
using Diaspora.Modules.Financial.Domain.Accounts;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Extensibility.Provisioning;
using Diaspora.Modules.Financial.Options;
using Diaspora.Platform.Services.Abstractions;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Verify to withdraw, not to receive.
///
/// <para>
/// <b>The defect these pin.</b> A wallet was activated only when its account was already Active, and
/// an account becomes Active on identity-verification approval. Every other layer disagreed:
/// <c>FinancialAccount.CanCredit</c> admits a pending account, the deposit handler says in as many
/// words that "a pending account may receive money and may not spend it, which is exactly right for
/// a deposit", and <c>CreatePaymentIntentCommandHandler</c> lets a pending account pay. Only the
/// wallet refused, because <c>Wallet.CanCredit</c> wants Active or Frozen.
/// </para>
///
/// <para>
/// The effect on the deployment was total: fourteen member accounts, every one Pending, so no member
/// could receive money at all. A creator earning could not be paid — the clearing sweep was refused
/// with "that wallet is closed" — and a card deposit would have been captured by the gateway and
/// then failed to post, taking money without crediting it.
/// </para>
///
/// <para>
/// <b>What must not move.</b> Withdrawal is gated on <c>account.CanDebit</c>, which is Active-only.
/// These assert both halves together, because loosening the receive side is only safe while the
/// withdraw side stays shut.
/// </para>
/// </summary>
public sealed class WalletActivationPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly IFinancialAccountRepository _accounts =
        Substitute.For<IFinancialAccountRepository>();

    private readonly IWalletRepository _wallets = Substitute.For<IWalletRepository>();

    // ── Receiving ───────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_wallet_opened_for_an_unverified_member_can_receive_money()
    {
        // The whole defect. Before this, a wallet on a pending account opened Pending and
        // Wallet.CanCredit refused it, so nothing could ever be paid in.
        var wallets = await Open(Unverified());

        wallets.Should().NotBeEmpty();

        wallets.Should().OnlyContain(
            wallet => wallet.CanCredit(Money.FromMinorUnits(1_000, "GBP", 2)),
            "an unverified member can still be paid");
    }

    [Fact]
    public async Task A_wallet_opened_for_an_unverified_member_is_active()
    {
        var wallets = await Open(Unverified());

        wallets.Should().OnlyContain(wallet => wallet.Status == WalletStatus.Active);
    }

    [Fact]
    public async Task A_wallet_opened_for_a_verified_member_is_active_too()
    {
        // Unchanged behaviour, asserted so the fix reads as a widening rather than a swap.
        var wallets = await Open(Verified());

        wallets.Should().OnlyContain(wallet => wallet.Status == WalletStatus.Active);
    }

    // ── Withdrawing ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_unverified_account_still_cannot_move_money_out()
    {
        // The gate that makes the above safe. The withdrawal handler refuses on exactly this.
        Unverified().CanDebit.Should().BeFalse();
    }

    [Fact]
    public void A_verified_account_can_move_money_out()
    {
        Verified().CanDebit.Should().BeTrue();
    }

    [Fact]
    public void The_account_level_credit_flag_is_not_what_admits_an_unverified_member()
    {
        // Worth pinning because it is the confusing part. `CanCredit` is Active-or-Restricted, so a
        // pending account answers false — and a pending member can still be paid, because the
        // deposit handler gates on Restricted rather than on this. Anybody tightening the deposit
        // handler to use CanCredit would shut the door this change opened.
        Unverified().CanCredit.Should().BeFalse(
            "a pending account is not yet creditable at the account level — the deposit handler "
            + "gates on Restricted rather than on CanCredit, which is what admits it");
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private async Task<IReadOnlyList<Wallet>> Open(FinancialAccount account)
    {
        var provisioning = Substitute.For<IWalletProvisioningPolicy>();
        provisioning
            .WalletsFor(Arg.Any<FinancialAccountType>(), Arg.Any<string>())
            .Returns([WalletType.Primary]);

        var guids = Substitute.For<IGuidGenerator>();
        guids.NewSequentialGuid().Returns(_ => Guid.NewGuid());

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        var identities = new FinancialIdentityService(
            _accounts,
            _wallets,
            provisioning,
            guids,
            clock,
            Microsoft.Extensions.Options.Options.Create(new FinancialOptions()));

        return await identities.EnsureWalletsAsync(account, "GBP");
    }

    private static FinancialAccount Unverified() =>
        FinancialAccount.Open(
            FinancialAccountId.New(),
            Guid.NewGuid(),
            FinancialAccountType.Personal,
            "GBP",
            "A member",
            Now);

    private static FinancialAccount Verified()
    {
        var account = Unverified();
        account.Activate(Now);

        return account;
    }
}
