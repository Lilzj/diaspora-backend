using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.ValueObjects;
using Diaspora.Modules.Financial.Domain.Withdrawals;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// The payout request's state machine and its three amounts.
/// <para>
/// <b>Every one of these is a rule about somebody's money that would otherwise live in a handler.</b>
/// The gross leaves the balance, the fee stays with the platform and the net arrives — three stored
/// figures rather than two and an assumption, because the one a client would derive is the one the
/// member cares about.
/// </para>
/// </summary>
public class WithdrawalTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_withdrawal_stores_its_gross_its_fee_and_its_net_separately()
    {
        var withdrawal = Request(gross: 10_000, fee: 250);

        withdrawal.Gross.MinorUnits.Should().Be(10_000);
        withdrawal.Fee.MinorUnits.Should().Be(250);
        withdrawal.Net.MinorUnits.Should().Be(9_750);
        withdrawal.Status.Should().Be(WithdrawalStatus.Requested);
        withdrawal.IsHeld.Should().BeTrue("the money is ring-fenced from the moment it is asked for");
    }

    /// <summary>
    /// A fee that swallowed the whole amount would send the member nothing, which is not a
    /// withdrawal.
    /// </summary>
    [Fact]
    public void A_fee_as_large_as_the_amount_is_refused()
    {
        var attempt = () => Request(gross: 500, fee: 500);

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.AmountInvalid);
    }

    [Fact]
    public void A_withdrawal_of_nothing_is_refused()
    {
        var attempt = () => Request(gross: 0, fee: 0);

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.AmountInvalid);
    }

    /// <summary>The platform never converts, and a fee in another currency is that by the back door.</summary>
    [Fact]
    public void A_fee_in_another_currency_is_refused()
    {
        var attempt = () => Withdrawal.Request(
            WithdrawalId.New(),
            Guid.NewGuid(),
            FinancialAccountId.New(),
            WalletId.New(),
            PayoutDestinationId.New(),
            "Barclays ••••4417",
            Money.FromMinorUnits(10_000, "GBP"),
            Money.FromMinorUnits(250, "USD"),
            Now);

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.CurrencyMismatch);
    }

    /// <summary>
    /// <b>Cancellation stops at the moment a provider takes the instruction.</b> Releasing a hold on
    /// money that may already be in flight is how a platform pays somebody twice.
    /// </summary>
    [Fact]
    public void A_withdrawal_a_provider_has_taken_cannot_be_cancelled()
    {
        var withdrawal = Request();

        withdrawal.MarkProcessing("test-settlement", "ref-1", Now);

        var attempt = () => withdrawal.Cancel(Now);

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.WithdrawalNotCancellable);
    }

    [Fact]
    public void A_cancelled_withdrawal_is_no_longer_held()
    {
        var withdrawal = Request();

        withdrawal.Cancel(Now);

        withdrawal.Status.Should().Be(WithdrawalStatus.Cancelled);
        withdrawal.IsHeld.Should().BeFalse();
        withdrawal.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void A_completed_withdrawal_cannot_be_completed_again()
    {
        var withdrawal = Request();

        withdrawal.MarkProcessing("test-settlement", "ref-1", Now);
        withdrawal.MarkCompleted(Now);

        var attempt = () => withdrawal.MarkCompleted(Now);

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.WithdrawalTransitionInvalid);
    }

    /// <summary>
    /// A reversal is a new fact rather than an undo. The withdrawal stays in the record as having
    /// happened, because the member's bank saw two movements and support has to explain both.
    /// </summary>
    [Fact]
    public void A_reversal_only_follows_a_completed_payout()
    {
        var withdrawal = Request();

        var tooEarly = () => withdrawal.MarkReversed("bounced", "The account was closed.", Now);

        tooEarly.Should().Throw<DomainException>();

        withdrawal.MarkProcessing("test-settlement", "ref-1", Now);
        withdrawal.MarkCompleted(Now);
        withdrawal.MarkReversed("bounced", "The account was closed.", Now.AddDays(1));

        withdrawal.Status.Should().Be(WithdrawalStatus.Reversed);
        withdrawal.FailureCode.Should().Be("bounced");
    }

    /// <summary>
    /// Every transition announces itself, so B13 can notify and automation can observe — and the
    /// announcement carries the masked label rather than anything that could reach a bank.
    /// <para>
    /// <b>Three events, not two, since BSEC-007 (10 Nov 2026).</b> This test's own summary has
    /// always said "every transition announces itself", and it asserted two after three state
    /// changes — so the sentence was true of the two it checked and false of the platform. The
    /// request raised nothing, which meant the one moment a member could still cancel a payout they
    /// did not ask for was the only moment in the lifecycle that told nobody.
    /// </para>
    /// </summary>
    [Fact]
    public void Every_transition_raises_an_event_carrying_only_the_masked_label()
    {
        var withdrawal = Request();

        withdrawal.DomainEvents.Should().HaveCount(
            1, "asking to take money out is itself a transition, and the first one");

        withdrawal.MarkProcessing("test-settlement", "ref-1", Now);
        withdrawal.MarkCompleted(Now);

        withdrawal.DomainEvents.Should().HaveCount(3);
        withdrawal.DomainEvents.Should().AllSatisfy(raised =>
            raised.Should().BeOfType<Modules.Financial.Domain.Events.WithdrawalStatusChangedDomainEvent>());

        var raised = withdrawal.DomainEvents
            .OfType<Modules.Financial.Domain.Events.WithdrawalStatusChangedDomainEvent>()
            .ToList();

        // The request: Requested, from nothing. A null previous status is how a consumer tells the
        // request apart from a later move, and it is the only event that can carry one.
        raised[0].Status.Should().Be(WithdrawalStatus.Requested);
        raised[0].PreviousStatus.Should().BeNull("the request had no status before it");

        raised.Skip(1).Should().OnlyContain(
            later => later.PreviousStatus != null, "every later move came from somewhere");

        var last = raised[^1];

        last.DestinationLabel.Should().Be("Barclays ••••4417");
        last.Status.Should().Be(WithdrawalStatus.Completed);

        raised.Should().OnlyContain(
            every => every.DestinationLabel == "Barclays ••••4417",
            "no event on this aggregate has ever carried anything a bank would accept, the new one "
            + "included");
    }

    private static Withdrawal Request(long gross = 10_000, long fee = 250) =>
        Withdrawal.Request(
            WithdrawalId.New(),
            Guid.NewGuid(),
            FinancialAccountId.New(),
            WalletId.New(),
            PayoutDestinationId.New(),
            "Barclays ••••4417",
            Money.FromMinorUnits(gross, "GBP"),
            Money.FromMinorUnits(fee, "GBP"),
            Now);
}

/// <summary>
/// The payout destination holds a handle and never a credential.
/// <para>
/// <b>Asserted by reflection rather than by reading the class</b>, because "we were careful not to
/// add a field for the account number" is exactly the kind of rule that survives until the next
/// author needs one.
/// </para>
/// </summary>
public class PayoutDestinationTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_payout_destination_has_no_property_that_could_hold_a_credential()
    {
        string[] forbidden =
        [
            "AccountNumber", "Iban", "SortCode", "RoutingNumber", "CardNumber", "Pan",
            "Cvv", "SecurityCode", "Bsb", "SwiftCode"
        ];

        var offenders = typeof(PayoutDestination)
            .GetProperties()
            .Select(property => property.Name)
            .Where(name => forbidden.Contains(name, StringComparer.OrdinalIgnoreCase))
            .ToList();

        offenders.Should().BeEmpty(
            "a platform that never stores an account number cannot leak one. Offenders: "
            + string.Join(", ", offenders));
    }

    [Fact]
    public void Nothing_pays_out_to_an_unverified_destination()
    {
        var destination = Register();

        destination.IsVerified.Should().BeFalse();
        destination.IsUsable.Should().BeFalse();

        destination.Verify(Now, "test-settlement", "tok_123");

        destination.IsUsable.Should().BeTrue();
    }

    /// <summary>
    /// Removal keeps the row — a withdrawal from six months ago still names it — and drops the
    /// provider token, because keeping it would be keeping the ability to send money there.
    /// </summary>
    [Fact]
    public void Removing_a_destination_keeps_the_record_and_drops_the_token()
    {
        var destination = Register();

        destination.Verify(Now, "test-settlement", "tok_123");
        destination.Remove(Now);

        destination.IsRemoved.Should().BeTrue();
        destination.IsUsable.Should().BeFalse();
        destination.ProviderToken.Should().BeNull();
        destination.Label.Should().Be("Barclays ••••4417", "old withdrawals still name it");
    }

    [Fact]
    public void A_removed_destination_cannot_be_verified()
    {
        var destination = Register();

        destination.Remove(Now);

        var attempt = () => destination.Verify(Now);

        attempt.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.DestinationNotFound);
    }

    private static PayoutDestination Register() =>
        PayoutDestination.Register(
            PayoutDestinationId.New(),
            Guid.NewGuid(),
            FinancialAccountId.New(),
            PayoutDestinationKind.BankAccount,
            "Barclays ••••4417",
            "GBP",
            Now);
}
