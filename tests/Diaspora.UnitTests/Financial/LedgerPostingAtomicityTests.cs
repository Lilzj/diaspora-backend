using Diaspora.Application.Abstractions;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Ledgers;
using Diaspora.Modules.Financial.Domain.Transactions;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Wallets;
using Diaspora.Modules.Financial.Infrastructure.Ledger;
using Diaspora.Platform.Services.Abstractions;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// A refused posting leaves no trace.
///
/// <para>
/// <b>The defect these pin, and it moved real money.</b> <c>PostAsync</c> checked a leg and
/// immediately applied it to its wallet. A posting whose <em>second</em> leg was refused returned a
/// failure having already moved the <em>first</em> wallet's balance: the entries were thrown away
/// and the transaction was never written, but the aggregate had been mutated and marked modified in
/// the change tracker.
/// </para>
///
/// <para>
/// Inside a request that is invisible — a failed command's unit of work is never saved. It is not
/// invisible to a caller that posts in a batch and saves at the end regardless. The creator earnings
/// sweep tried to pay a member whose wallet had been provisioned but never activated, was refused on
/// the credit leg, and then saved: the platform's revenue wallet lost 100,000 minor units with no
/// ledger entry behind it. The ledger and the balance disagreed, which is the one thing this service
/// exists to prevent.
/// </para>
///
/// <para>
/// <b>Why no test saw it.</b> The service had no unit tests at all — it is exercised through
/// integration tests that post successfully. Nothing asserted on the state of the world after a
/// refusal, because a refusal was assumed to be inert.
/// </para>
/// </summary>
public sealed class LedgerPostingAtomicityTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly IWalletRepository _wallets = Substitute.For<IWalletRepository>();
    private readonly ILedgerEntryRepository _entries = Substitute.For<ILedgerEntryRepository>();

    private readonly IFinancialTransactionRepository _transactions =
        Substitute.For<IFinancialTransactionRepository>();

    // ── The defect ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_leg_refused_after_another_has_passed_leaves_the_first_wallet_alone()
    {
        // The exact shape that lost the money: debit a funded wallet, credit one that cannot
        // receive. The credit is refused, and the debit must not have happened.
        var source = Funded(10_000);
        var destination = NotYetActive();

        var result = await Post(Debit(source, 5_000), Credit(destination, 5_000));

        result.IsFailure.Should().BeTrue();
        source.AvailableMinorUnits.Should().Be(10_000, "nothing was posted, so nothing moved");
    }

    [Fact]
    public async Task A_refused_posting_does_not_mark_a_wallet_modified()
    {
        // The balance being right in memory is not enough. The sweep's unit of work saves whatever
        // the change tracker holds, so a wallet marked modified is a wallet written back.
        var source = Funded(10_000);
        var destination = NotYetActive();

        await Post(Debit(source, 5_000), Credit(destination, 5_000));

        _wallets.DidNotReceive().Update(source);
    }

    [Fact]
    public async Task A_refused_posting_writes_neither_a_transaction_nor_an_entry()
    {
        var source = Funded(10_000);
        var destination = NotYetActive();

        await Post(Debit(source, 5_000), Credit(destination, 5_000));

        _transactions.DidNotReceiveWithAnyArgs().Add(default!);
        _entries.DidNotReceiveWithAnyArgs().AddRange(default!);
    }

    [Fact]
    public async Task The_lifetime_totals_are_untouched_by_a_refusal()
    {
        // Apply moves more than the balance — lifetime debit, the entry count and the revision all
        // shift with it, and a half-applied posting corrupts every one of them.
        var source = Funded(10_000);
        var destination = NotYetActive();

        var countBefore = source.EntryCount;

        await Post(Debit(source, 5_000), Credit(destination, 5_000));

        source.LifetimeDebitMinorUnits.Should().Be(0);
        source.EntryCount.Should().Be(countBefore);
    }

    // ── What must still work ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task A_posting_where_every_leg_passes_still_moves_the_money()
    {
        var source = Funded(10_000);
        var destination = Funded(0);

        var result = await Post(Debit(source, 5_000), Credit(destination, 5_000));

        result.IsSuccess.Should().BeTrue();
        source.AvailableMinorUnits.Should().Be(5_000);
        destination.AvailableMinorUnits.Should().Be(5_000);
    }

    [Fact]
    public async Task Two_legs_against_one_wallet_still_fold_against_each_other()
    {
        // The behaviour the old loop existed to provide, and the reason the fix projects rather than
        // simply validating everything first: the second leg must compute against the first, not
        // against the balance the posting started with.
        var wallet = Funded(1_000);
        var other = Funded(10_000);

        var result = await Post(
            Credit(wallet, 5_000),
            Debit(wallet, 4_000),
            Debit(other, 5_000),
            Credit(other, 4_000));

        result.IsSuccess.Should().BeTrue();
        wallet.AvailableMinorUnits.Should().Be(2_000, "1,000 + 5,000 - 4,000");
    }

    [Fact]
    public async Task A_debit_covered_only_by_an_earlier_credit_in_the_same_posting_is_allowed()
    {
        // 500 on hand, credited 5,000, then debited 5,200. It only clears because the credit is
        // folded forward first — checking each leg against the starting balance would refuse it.
        var wallet = Funded(500);
        var other = Funded(10_000);

        var result = await Post(
            Credit(wallet, 5_000),
            Debit(wallet, 5_200),
            Debit(other, 5_000),
            Credit(other, 5_200));

        result.IsSuccess.Should().BeTrue();
        wallet.AvailableMinorUnits.Should().Be(300);
    }

    [Fact]
    public async Task A_debit_the_fold_cannot_cover_is_still_refused()
    {
        var wallet = Funded(500);
        var other = Funded(10_000);

        var result = await Post(
            Credit(wallet, 1_000),
            Debit(wallet, 5_000),
            Debit(other, 1_000),
            Credit(other, 5_000));

        result.IsFailure.Should().BeTrue();
        wallet.AvailableMinorUnits.Should().Be(500, "and the credit that preceded it is gone too");
    }

    [Fact]
    public async Task Each_leg_on_one_wallet_gets_its_own_sequence()
    {
        // Sequences come from the projection now rather than from the aggregate, and two entries
        // sharing one would break the wallet's own replay check on Apply.
        var wallet = Funded(1_000);
        var other = Funded(10_000);

        var result = await Post(
            Credit(wallet, 5_000),
            Debit(wallet, 4_000),
            Debit(other, 5_000),
            Credit(other, 4_000));

        result.Value.Entries
            .Where(entry => entry.WalletId == wallet.Id)
            .Select(entry => entry.Sequence)
            .Should().OnlyHaveUniqueItems();
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private LedgerPostingService Service()
    {
        var guids = Substitute.For<IGuidGenerator>();
        guids.NewSequentialGuid().Returns(_ => Guid.NewGuid());

        var clock = Substitute.For<IClock>();
        clock.UtcNow.Returns(Now);

        return new LedgerPostingService(_wallets, _entries, _transactions, guids, clock);
    }

    private Task<Diaspora.SharedKernel.Results.Result<LedgerPostingResult>> Post(
        params LedgerPostingLeg[] legs) =>
        Service().PostAsync(new LedgerPostingRequest
        {
            TransactionType = TransactionType.Payout,
            Amount = Money.FromMinorUnits(legs[0].Amount.MinorUnits, "GBP", 2),
            Description = "A movement",
            Audit = AuditStamp.Create(
                FinancialActor.System("test"), FinancialSource.System, "A movement"),
            Legs = legs
        });

    private Wallet Funded(long minorUnits)
    {
        var wallet = FinancialTestData.Wallet_();

        if (minorUnits > 0)
        {
            // Through the aggregate, so the balance under test is one the domain would produce.
            wallet.Apply(LedgerEntry.Append(
                LedgerEntryId.New(),
                wallet.Id,
                TransactionId.New(),
                wallet.NextSequence,
                LedgerEntryType.Credit,
                LedgerDirection.Credit,
                LedgerBucket.Available,
                Money.FromMinorUnits(minorUnits, "GBP", 2),
                0,
                FinancialTestData.Stamp(),
                FinancialTestData.Now));
        }

        Register(wallet);
        return wallet;
    }

    /// <summary>
    /// Provisioned and never activated — the state every member wallet on the deployment was in.
    /// <para>
    /// It is the ordinary case rather than an exotic one, which is why this defect fired the first
    /// time anything tried to pay a member.
    /// </para>
    /// </summary>
    private Wallet NotYetActive()
    {
        var wallet = FinancialTestData.Wallet_(active: false);

        Register(wallet);
        return wallet;
    }

    private void Register(Wallet wallet) =>
        _wallets.FindAsync(wallet.Id, Arg.Any<CancellationToken>()).Returns(wallet);

    private static LedgerPostingLeg Debit(Wallet wallet, long minorUnits) => new()
    {
        WalletId = wallet.Id,
        Direction = LedgerDirection.Debit,
        Amount = Money.FromMinorUnits(minorUnits, "GBP", 2),
        EntryType = LedgerEntryType.Payout
    };

    private static LedgerPostingLeg Credit(Wallet wallet, long minorUnits) => new()
    {
        WalletId = wallet.Id,
        Direction = LedgerDirection.Credit,
        Amount = Money.FromMinorUnits(minorUnits, "GBP", 2),
        EntryType = LedgerEntryType.Payout
    };
}
