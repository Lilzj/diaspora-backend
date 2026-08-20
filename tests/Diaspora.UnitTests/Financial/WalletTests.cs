using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// The wallet is a projection of the ledger and never a source of truth.
/// <para>
/// The first test in this class is the architectural claim of the whole phase, expressed as a
/// reflection assertion: there is no way to set a balance.
/// </para>
/// </summary>
public class WalletTests
{
    /// <summary>
    /// <b>The claim, enforced.</b> No <c>Credit</c>, no <c>Debit</c>, no <c>Deposit</c>, no
    /// <c>AdjustBalance</c> — the only public member that moves a balance takes a
    /// <see cref="LedgerEntry"/>.
    /// </summary>
    [Fact]
    public void There_is_no_way_to_set_a_balance_on_a_wallet()
    {
        var forbidden = new[]
        {
            "Credit", "Debit", "Deposit", "Withdraw", "SetBalance", "AdjustBalance",
            "AddFunds", "RemoveFunds", "SetAvailable", "SetPending", "SetReserved"
        };

        var offenders = typeof(Wallet)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(method => method.Name)
            .Where(name => forbidden.Contains(name, StringComparer.Ordinal))
            .ToList();

        offenders.Should().BeEmpty(
            "balances move because entries are appended, and for no other reason");

        var publicBalanceSetters = typeof(Wallet)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.Name.EndsWith("MinorUnits", StringComparison.Ordinal))
            .Where(property => property.SetMethod?.IsPublic == true)
            .Select(property => property.Name)
            .ToList();

        publicBalanceSetters.Should().BeEmpty("a balance is never assigned from outside");
    }

    [Fact]
    public void Applying_a_credit_moves_the_bucket_and_the_lifetime_total()
    {
        var wallet = FinancialTestData.Wallet_();

        wallet.Apply(Entry(wallet, LedgerDirection.Credit, 2500));

        wallet.AvailableMinorUnits.Should().Be(2500);
        wallet.LifetimeCreditMinorUnits.Should().Be(2500);
        wallet.LifetimeDebitMinorUnits.Should().Be(0);
        wallet.EntryCount.Should().Be(1);
    }

    [Fact]
    public void Applying_a_debit_moves_the_other_lifetime_total()
    {
        var wallet = FinancialTestData.Wallet_();

        wallet.Apply(Entry(wallet, LedgerDirection.Credit, 2500));
        wallet.Apply(Entry(wallet, LedgerDirection.Debit, 500));

        wallet.AvailableMinorUnits.Should().Be(2000);
        wallet.LifetimeDebitMinorUnits.Should().Be(500);
    }

    /// <summary>A hold is a debit of available and a credit of reserved, in one transaction.</summary>
    [Fact]
    public void A_hold_moves_money_between_buckets_and_leaves_the_total_alone()
    {
        var wallet = FinancialTestData.Wallet_();

        wallet.Apply(Entry(wallet, LedgerDirection.Credit, 5000));
        wallet.Apply(Entry(wallet, LedgerDirection.Debit, 2000, LedgerBucket.Available));
        wallet.Apply(Entry(wallet, LedgerDirection.Credit, 2000, LedgerBucket.Reserved));

        wallet.AvailableMinorUnits.Should().Be(3000);
        wallet.ReservedMinorUnits.Should().Be(2000);
    }

    /// <summary>
    /// An entry appended against a stale read is precisely the corruption a projection cannot detect
    /// later, so it is refused at the moment it is applied.
    /// </summary>
    [Fact]
    public void An_entry_computed_against_a_stale_balance_is_refused()
    {
        var wallet = FinancialTestData.Wallet_();
        var stale = Entry(wallet, LedgerDirection.Credit, 100, balanceBefore: 999);

        var apply = () => wallet.Apply(stale);

        apply.Should().Throw<InvalidOperationException>().WithMessage("*this wallet holds*");
    }

    [Fact]
    public void An_entry_for_another_wallet_is_refused()
    {
        var wallet = FinancialTestData.Wallet_();
        var other = FinancialTestData.Wallet_();

        var apply = () => wallet.Apply(Entry(other, LedgerDirection.Credit, 100));

        apply.Should().Throw<InvalidOperationException>().WithMessage("*different wallet*");
    }

    /// <summary>The recovery path, and the proof the projection is one.</summary>
    [Fact]
    public void A_wallet_rebuilds_to_the_same_numbers_from_its_entries_alone()
    {
        var wallet = FinancialTestData.Wallet_();

        var entries = new List<LedgerEntry>
        {
            Entry(wallet, LedgerDirection.Credit, 5000, balanceBefore: 0, sequence: 1),
            Entry(wallet, LedgerDirection.Debit, 1500, balanceBefore: 5000, sequence: 2),
            Entry(wallet, LedgerDirection.Credit, 250, balanceBefore: 3500, sequence: 3)
        };

        foreach (var entry in entries)
        {
            wallet.Apply(entry);
        }

        var expected = wallet.AvailableMinorUnits;

        wallet.RebuildFrom(entries);

        wallet.AvailableMinorUnits.Should().Be(expected);
        wallet.LifetimeCreditMinorUnits.Should().Be(5250);
        wallet.LifetimeDebitMinorUnits.Should().Be(1500);
        wallet.EntryCount.Should().Be(3);
    }

    [Fact]
    public void A_frozen_wallet_accepts_money_and_refuses_to_let_any_leave()
    {
        var wallet = FinancialTestData.Wallet_();

        wallet.Apply(Entry(wallet, LedgerDirection.Credit, 5000));
        wallet.Freeze("Under review.", FinancialTestData.Now);

        wallet.CanCredit(FinancialTestData.Gbp_(10m)).Should().BeTrue();
        wallet.CanDebit(FinancialTestData.Gbp_(10m)).Should().BeFalse();
    }

    /// <summary>
    /// A closed wallet with a reserved balance is money nobody can reach. The refusal is on the
    /// aggregate because it is the only thing that can see the balance.
    /// </summary>
    [Fact]
    public void A_wallet_holding_reserved_funds_cannot_be_closed()
    {
        var wallet = FinancialTestData.Wallet_();

        wallet.Apply(Entry(wallet, LedgerDirection.Credit, 1000, LedgerBucket.Reserved));

        var close = () => wallet.Close(FinancialTestData.Now);

        close.Should().Throw<DomainException>().WithMessage("*Release them first*");
    }

    [Fact]
    public void A_closed_wallet_cannot_be_reopened()
    {
        var wallet = FinancialTestData.Wallet_();

        wallet.Close(FinancialTestData.Now);

        var reopen = () => wallet.Activate(FinancialTestData.Now);

        reopen.Should().Throw<DomainException>().WithMessage("*Open a new one*");
    }

    [Fact]
    public void Only_the_platform_clearing_wallet_may_run_negative()
    {
        FinancialTestData.ClearingWallet().AllowsNegativeBalance.Should().BeTrue();
        FinancialTestData.Wallet_().AllowsNegativeBalance.Should().BeFalse();
        FinancialTestData.Wallet_(WalletType.Promotional).AllowsNegativeBalance.Should().BeFalse();
    }

    [Fact]
    public void Creating_a_wallet_announces_it()
    {
        FinancialTestData.Wallet_(active: false).DomainEvents.Should().ContainSingle();
    }

    private static LedgerEntry Entry(
        Wallet wallet,
        LedgerDirection direction,
        long minorUnits,
        LedgerBucket bucket = LedgerBucket.Available,
        long? balanceBefore = null,
        long? sequence = null) =>
        LedgerEntry.Append(
            LedgerEntryId.New(),
            wallet.Id,
            TransactionId.New(),
            sequence ?? wallet.NextSequence,
            LedgerEntryType.Payment,
            direction,
            bucket,
            Money.FromMinorUnits(minorUnits, wallet.Currency),
            balanceBefore ?? wallet.BalanceOf(bucket),
            FinancialTestData.Stamp(),
            FinancialTestData.Now,
            allowNegativeBalance: wallet.AllowsNegativeBalance);
}
