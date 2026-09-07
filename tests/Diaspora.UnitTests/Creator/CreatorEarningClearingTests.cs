using Diaspora.Modules.Creator.Application.Earnings;
using Diaspora.Modules.Creator.Domain.Earnings;
using Diaspora.Modules.Creator.Domain.Enums;
using Diaspora.Modules.Creator.Domain.Identifiers;
using Diaspora.Modules.Creator.Domain.Repositories;
using Diaspora.Modules.Creator.Options;
using Diaspora.Modules.Financial.Extensibility.Disbursements;
using Diaspora.SharedKernel.Results;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Diaspora.UnitTests.Creator;

/// <summary>
/// A creator's earning becoming money they can reach.
///
/// <para>
/// <b>The defect these pin.</b> Nothing moved an earning past <c>Pending</c>. <c>MakeAvailable</c>,
/// <c>AttachToPayout</c> and <c>MarkPaid</c> existed on the aggregate, were fully unit-tested, and
/// had <em>no caller in production code</em> — only tests. <c>ListClearableAsync</c> was written and
/// never asked. <c>CreatorLifecycleSweep</c> ran hourly and swept verifications and referrals.
/// </para>
///
/// <para>
/// On a device that read as: a revenue dashboard showing <b>£1,000 pending</b>, the sentence
/// "Clears 30 days after it is earned", and a row saying "Clears on Oct 7" — a promise nothing kept.
/// On 7 October it would have said PENDING, and on every day after. The money sat in the platform's
/// revenue wallet with no path out, beside a Withdraw button with nothing behind it.
/// </para>
///
/// <para>
/// <b>Why the whole suite passed over it.</b> The aggregate's lifecycle is exercised directly by
/// <c>CreatorEarningTests</c> — call <c>MakeAvailable</c>, assert the state changed. That tests the
/// transition, not that anything performs it. A complete, correct, well-tested piece of domain with
/// no producer driving it is invisible to every test that calls it itself.
/// </para>
/// </summary>
public sealed class CreatorEarningClearingTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
    private static readonly Guid Member = Guid.NewGuid();

    private readonly ICreatorEarningRepository _earnings = Substitute.For<ICreatorEarningRepository>();
    private readonly IPlatformDisbursement _disbursements = Substitute.For<IPlatformDisbursement>();

    // ── The step that was missing ───────────────────────────────────────────────────────────

    [Fact]
    public async Task A_cleared_earning_is_paid_and_becomes_available()
    {
        var earning = Pending(share: 100_000);
        GivenClearable(earning);
        GivenPaymentSucceeds();

        var cleared = await Clearing().ClearAsync(Now, batchSize: 50);

        cleared.Should().Be(1);
        earning.State.Should().Be(CreatorEarningState.Available);
    }

    [Fact]
    public async Task It_pays_the_creators_share_and_not_the_gross()
    {
        // The gross is what the donor gave. A platform cut, where one is configured, never belonged
        // to the creator — paying the gross would hand out money the platform is owed.
        var earning = Pending(share: 70_000, gross: 100_000);
        GivenClearable(earning);
        GivenPaymentSucceeds();

        await Clearing().ClearAsync(Now, batchSize: 50);

        await _disbursements.Received(1).PayAsync(
            Arg.Is<PlatformDisbursementRequest>(request => request.AmountMinorUnits == 70_000),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task It_pays_the_member_who_earned_it_in_their_own_currency()
    {
        var earning = Pending(share: 100_000);
        GivenClearable(earning);
        GivenPaymentSucceeds();

        await Clearing().ClearAsync(Now, batchSize: 50);

        await _disbursements.Received(1).PayAsync(
            Arg.Is<PlatformDisbursementRequest>(request =>
                request.BeneficiaryUserId == Member
                && request.Currency == "GBP"
                && request.Exponent == 2),
            Arg.Any<CancellationToken>());
    }

    // ── Paying twice ────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task The_payment_is_keyed_on_the_earning_so_a_retried_pass_cannot_pay_twice()
    {
        // The reference is the whole safety property. A pass that dies after paying and before
        // saving runs again over the same rows; without a stable key it would pay every one of them
        // a second time. The ledger answers an existing reference rather than posting again.
        var earning = Pending(share: 100_000);
        GivenClearable(earning);
        GivenPaymentSucceeds();

        await Clearing().ClearAsync(Now, batchSize: 50);

        await _disbursements.Received(1).PayAsync(
            Arg.Is<PlatformDisbursementRequest>(request =>
                request.Reference == $"earning:{earning.Id.Value:N}"),
            Arg.Any<CancellationToken>());
    }

    // ── The order of the two steps ──────────────────────────────────────────────────────────

    [Fact]
    public async Task A_payment_that_fails_leaves_the_earning_pending()
    {
        // The order is the design: money first, state second. Marking it available and then failing
        // to pay would tell a member the money was in their wallet when it was not — and the row
        // would never be looked at again, because nothing sweeps Available.
        var earning = Pending(share: 100_000);
        GivenClearable(earning);

        _disbursements
            .PayAsync(Arg.Any<PlatformDisbursementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PlatformDisbursementReceipt>(
                Error.Unexpected("financial.wallet.frozen", "That wallet is frozen.")));

        var cleared = await Clearing().ClearAsync(Now, batchSize: 50);

        cleared.Should().Be(0);
        earning.State.Should().Be(CreatorEarningState.Pending, "the next pass must try it again");
    }

    [Fact]
    public async Task One_failure_does_not_stop_the_others_being_paid()
    {
        // A frozen wallet is one member's problem. Abandoning the pass would hold up everybody
        // else's money behind it until somebody noticed.
        var frozen = Pending(share: 100_000);
        var fine = Pending(share: 50_000);
        GivenClearable(frozen, fine);

        _disbursements
            .PayAsync(
                Arg.Is<PlatformDisbursementRequest>(request => request.AmountMinorUnits == 100_000),
                Arg.Any<CancellationToken>())
            .Returns(Result.Failure<PlatformDisbursementReceipt>(
                Error.Unexpected("financial.wallet.frozen", "That wallet is frozen.")));

        _disbursements
            .PayAsync(
                Arg.Is<PlatformDisbursementRequest>(request => request.AmountMinorUnits == 50_000),
                Arg.Any<CancellationToken>())
            .Returns(Receipt());

        var cleared = await Clearing().ClearAsync(Now, batchSize: 50);

        cleared.Should().Be(1);
        frozen.State.Should().Be(CreatorEarningState.Pending);
        fine.State.Should().Be(CreatorEarningState.Available);
    }

    // ── The clearing period ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task It_asks_for_earnings_older_than_the_configured_period()
    {
        // Read from the same option the response computes "Clears on Oct 7" from. A second copy of
        // "thirty days" here is how a screen and a sweep come to disagree about a date the member is
        // holding the platform to.
        GivenClearable();

        await Clearing(clearingDays: 30).ClearAsync(Now, batchSize: 50);

        await _earnings.Received(1).ListClearableAsync(
            Now.AddDays(-30), 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_clearing_period_of_zero_clears_immediately()
    {
        // The configuration a deployment uses to prove the path without waiting a month.
        GivenClearable();

        await Clearing(clearingDays: 0).ClearAsync(Now, batchSize: 50);

        await _earnings.Received(1).ListClearableAsync(Now, 50, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Nothing_to_clear_pays_nobody()
    {
        GivenClearable();

        var cleared = await Clearing().ClearAsync(Now, batchSize: 50);

        cleared.Should().Be(0);
        await _disbursements.DidNotReceiveWithAnyArgs().PayAsync(default!, default);
    }

    // ── Fixtures ────────────────────────────────────────────────────────────────────────────

    private CreatorEarningClearing Clearing(int clearingDays = 30) =>
        new(_earnings,
            _disbursements,
            Options.Create(new CreatorRevenueShareOptions { ClearingDays = clearingDays }),
            NullLogger<CreatorEarningClearing>.Instance);

    private static CreatorEarning Pending(long share, long gross = 100_000) =>
        CreatorEarning.Record(
            CreatorId.New(),
            Member,
            CreatorEarningSource.Donation,
            share,
            gross,
            "GBP",
            2,
            $"donation.completed:{Guid.NewGuid():N}",
            "campaign",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Now.AddDays(-31)).Value;

    private void GivenClearable(params CreatorEarning[] earnings) =>
        _earnings
            .ListClearableAsync(
                Arg.Any<DateTimeOffset>(), Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns(earnings);

    private void GivenPaymentSucceeds() =>
        _disbursements
            .PayAsync(Arg.Any<PlatformDisbursementRequest>(), Arg.Any<CancellationToken>())
            .Returns(Receipt());

    private static Result<PlatformDisbursementReceipt> Receipt() =>
        Result.Success(new PlatformDisbursementReceipt
        {
            TransactionId = Guid.NewGuid(),
            Posted = true
        });
}
