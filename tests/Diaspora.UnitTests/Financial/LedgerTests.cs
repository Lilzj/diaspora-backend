using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using System.Reflection;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// The canonical financial record.
/// <para>
/// The immutability tests here are the ones that matter. An entry that could be edited is a ledger
/// nobody can rely on, and the edit is invisible precisely when it matters.
/// </para>
/// </summary>
public class LedgerTests
{
    [Fact]
    public void An_entry_computes_the_balance_after_rather_than_accepting_it()
    {
        var entry = Append(LedgerDirection.Credit, 2500, balanceBefore: 1000);

        entry.BalanceBeforeMinorUnits.Should().Be(1000);
        entry.BalanceAfterMinorUnits.Should().Be(3500);
    }

    [Fact]
    public void A_debit_reduces_the_balance()
    {
        Append(LedgerDirection.Debit, 400, balanceBefore: 1000)
            .BalanceAfterMinorUnits.Should().Be(600);
    }

    [Fact]
    public void An_entry_that_would_take_a_bucket_negative_is_refused()
    {
        var append = () => Append(LedgerDirection.Debit, 1001, balanceBefore: 1000);

        append.Should().Throw<InvalidOperationException>()
            .WithMessage("*never negative*");
    }

    /// <summary>
    /// One nominal account has to be allowed to run negative or nothing balances. It is the
    /// platform's clearing wallet and nothing else.
    /// </summary>
    [Fact]
    public void The_clearing_exemption_lets_one_account_run_negative()
    {
        var entry = Append(
            LedgerDirection.Debit, 1000, balanceBefore: 0, allowNegativeBalance: true);

        entry.BalanceAfterMinorUnits.Should().Be(-1000);
    }

    [Fact]
    public void The_signed_effect_carries_the_direction()
    {
        Append(LedgerDirection.Credit, 500, 0).SignedMinorUnits.Should().Be(500);
        Append(LedgerDirection.Debit, 500, 500).SignedMinorUnits.Should().Be(-500);
    }

    [Fact]
    public void A_sequence_must_be_positive()
    {
        var append = () => LedgerEntry.Append(
            LedgerEntryId.New(),
            WalletId.New(),
            TransactionId.New(),
            sequence: 0,
            LedgerEntryType.Credit,
            LedgerDirection.Credit,
            LedgerBucket.Available,
            FinancialTestData.Gbp_(1m),
            0,
            FinancialTestData.Stamp(),
            FinancialTestData.Now);

        append.Should().Throw<ArgumentOutOfRangeException>();
    }

    /// <summary>
    /// <b>The immutability claim, asserted rather than documented.</b> One factory, and no public
    /// member that changes anything — so a future author who adds a setter fails here rather than a
    /// reconciliation.
    /// </summary>
    [Fact]
    public void An_entry_has_no_public_way_to_change_anything()
    {
        var setters = typeof(LedgerEntry)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => property.SetMethod is { IsPublic: true })
            .Select(property => property.Name)
            .ToList();

        setters.Should().BeEmpty("the ledger is append-only");

        var mutators = typeof(LedgerEntry)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .ToList();

        mutators.Should().BeEmpty(
            "correcting the ledger means appending a compensating entry, never editing one");
    }

    /// <summary>
    /// <c>ISoftDeletable</c> and <c>IAuditableEntity</c> both exist to record that something changed.
    /// Nothing about a ledger entry ever does.
    /// </summary>
    [Fact]
    public void An_entry_is_neither_soft_deletable_nor_auditable()
    {
        typeof(Diaspora.Domain.Abstractions.ISoftDeletable)
            .IsAssignableFrom(typeof(LedgerEntry)).Should().BeFalse();

        typeof(Diaspora.Domain.Abstractions.IAuditableEntity)
            .IsAssignableFrom(typeof(LedgerEntry)).Should().BeFalse();
    }

    [Fact]
    public void Every_entry_carries_an_audit_stamp_with_a_reason()
    {
        Append(LedgerDirection.Credit, 100, 0).Audit.Reason.Should().NotBeNullOrWhiteSpace();
    }

    /// <summary>
    /// Recomputing a historical amount from today's policy produces a plausible number and a wrong
    /// one, so the version that decided it travels with the entry.
    /// </summary>
    [Fact]
    public void A_stamp_can_carry_the_policy_version_that_decided_the_amount()
    {
        var stamped = FinancialTestData.Stamp().WithPolicyVersion("marketplace.commission@2");

        stamped.PolicyVersion.Should().Be("marketplace.commission@2");
    }

    private static LedgerEntry Append(
        LedgerDirection direction,
        long minorUnits,
        long balanceBefore,
        bool allowNegativeBalance = false) =>
        LedgerEntry.Append(
            LedgerEntryId.New(),
            WalletId.New(),
            TransactionId.New(),
            sequence: 1,
            LedgerEntryType.Payment,
            direction,
            LedgerBucket.Available,
            Diaspora.Modules.Financial.Domain.ValueObjects.Money.FromMinorUnits(minorUnits, "GBP"),
            balanceBefore,
            FinancialTestData.Stamp(),
            FinancialTestData.Now,
            allowNegativeBalance: allowNegativeBalance);
}
