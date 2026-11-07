using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Application.Operations;
using Diaspora.Modules.Financial.Domain.Credits;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Events;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Payments;
using Diaspora.SharedKernel.Results;
using Diaspora.UnitTests.Financial.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// Staff decisions about members' money need a reason, a guard and a way back (7 Nov 2026).
/// <para>
/// The promotional credit guard and cap, the reasons a wallet keeps, and the refund request an appeal
/// reopens. The routes behind them are proved end to end in the integration suite's
/// <c>MoneyDueProcessTests</c>.
/// </para>
/// </summary>
public sealed class MoneyDueProcessTests
{
    private static readonly Guid Staff = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid Member = Guid.Parse("11111111-1111-1111-1111-111111111111");

    // ── Promotional credit: the guard and the cap ────────────────────────────────────────────

    [Fact]
    public void Nobody_grants_credit_to_themselves()
    {
        var refusal = CreditGrantPolicy.RefuseSelf(Staff, Staff);

        refusal.Should().NotBeNull();
        refusal!.Code.Should().Be(FinancialErrorCodes.CreditGrantToSelf);
        refusal.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public void Granting_to_somebody_else_is_not_refused_as_a_self_grant()
    {
        CreditGrantPolicy.RefuseSelf(Staff, Member).Should().BeNull();
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(50)]
    [InlineData(100)]
    public void A_grant_up_to_the_cap_is_allowed(double amount)
    {
        CreditGrantPolicy.RefuseAmount((decimal)amount).Should().BeNull();
    }

    [Theory]
    [InlineData(100.01)]
    [InlineData(500)]
    [InlineData(1_000_000)]
    public void A_grant_above_the_cap_is_refused_whoever_approves_it(double amount)
    {
        var refusal = CreditGrantPolicy.RefuseAmount((decimal)amount);

        refusal.Should().NotBeNull();
        refusal!.Code.Should().Be(FinancialErrorCodes.CreditGrantAboveMaximum);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void A_grant_of_nothing_or_less_is_refused(double amount)
    {
        CreditGrantPolicy.RefuseAmount((decimal)amount)!.Code.Should().Be(FinancialErrorCodes.AmountInvalid);
    }

    [Theory]
    [InlineData(10, false)]
    [InlineData(50, false)]
    [InlineData(50.01, true)]
    [InlineData(100, true)]
    public void Above_the_threshold_a_second_admin_must_approve(double amount, bool needsApproval)
    {
        CreditGrantPolicy.NeedsApproval((decimal)amount).Should().Be(needsApproval);
    }

    [Fact]
    public void The_threshold_is_below_the_cap()
    {
        CreditGrantPolicy.ApprovalThresholdMajorUnits.Should().BeLessThan(CreditGrantPolicy.MaximumMajorUnits);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("goodwill")]
    public void A_grant_needs_a_reason_a_member_can_read(string? reason)
    {
        CreditGrantPolicy.RefuseReason(reason)!.Code.Should().Be(FinancialErrorCodes.CreditGrantReasonRequired);
    }

    [Fact]
    public void A_real_reason_is_accepted()
    {
        CreditGrantPolicy.RefuseReason("Compensation for the outage on 3 November.").Should().BeNull();
    }

    // ── Credit grant requests ────────────────────────────────────────────────────────────────

    [Fact]
    public void A_credit_grant_request_starts_pending_and_lands_once()
    {
        var request = CreditGrantRequest.Raise(
            CreditGrantRequestId.New(), Member, FinancialTestData.Gbp_(80m), CreditKind.Compensation,
            " Compensation for the outage ", null, Staff, FinancialTestData.Now);

        request.Status.Should().Be(CreditGrantRequestStatus.Pending);
        request.Reason.Should().Be("Compensation for the outage");

        var approver = Guid.NewGuid();
        var grant = CreditGrantId.New();
        request.Approve(approver, null, grant, FinancialTestData.Now);

        request.Status.Should().Be(CreditGrantRequestStatus.Approved);
        request.DecidedByUserId.Should().Be(approver);
        request.CreditGrantId.Should().Be(grant);

        var again = () => request.Approve(approver, null, CreditGrantId.New(), FinancialTestData.Now);
        again.Should().Throw<DomainException>().Which.Code.Should().Be(FinancialErrorCodes.CreditGrantRequestDecided);
    }

    // ── Wallet reasons ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_closed_wallet_keeps_the_reason()
    {
        var wallet = FinancialTestData.Wallet_();

        wallet.Close(FinancialTestData.Now, " The member asked us to close it. ");

        wallet.Status.Should().Be(WalletStatus.Closed);
        wallet.StatusReason.Should().Be("The member asked us to close it.");
        wallet.DomainEvents.OfType<WalletStatusChangedDomainEvent>().Last().Reason
            .Should().Be("The member asked us to close it.");
    }

    [Fact]
    public void A_lifted_freeze_carries_the_reason_it_was_lifted()
    {
        var wallet = FinancialTestData.Wallet_();
        wallet.Freeze("Two deposits were reversed by the issuer.", FinancialTestData.Now);

        wallet.Activate(FinancialTestData.Now, "Upheld on appeal: the deposits were genuine.");

        wallet.Status.Should().Be(WalletStatus.Active);
        wallet.StatusReason.Should().Be("Upheld on appeal: the deposits were genuine.");
    }

    [Fact]
    public void Opening_a_wallet_still_needs_no_reason()
    {
        var wallet = FinancialTestData.Wallet_();

        wallet.StatusReason.Should().BeNull();
    }

    // ── A refund request reopened on appeal ──────────────────────────────────────────────────

    [Fact]
    public void A_refused_request_reopens_for_somebody_else_to_decide()
    {
        var refuser = Guid.NewGuid();
        var request = Refused(refuser);

        request.ReopenOnAppeal(FinancialTestData.Now.AddDays(2));

        request.Status.Should().Be(RefundRequestStatus.Requested);
        request.IsOpen.Should().BeTrue("one open request per payment is enforced on this column");
        request.OpenForPaymentIntentId.Should().Be(request.PaymentIntentId.Value);
        request.ReopenedAt.Should().Be(FinancialTestData.Now.AddDays(2));
        request.DecidedByUserId.Should().BeNull();
        request.DecisionNote.Should().BeNull();
        request.WasRefusedBy(refuser).Should().BeTrue();
        request.WasRefusedBy(Guid.NewGuid()).Should().BeFalse();
    }

    [Fact]
    public void Only_a_refusal_can_be_reopened()
    {
        var request = RefundRequest.Raise(
            RefundRequestId.New(), PaymentIntentId.New(), Member, FinancialTestData.Gbp_(20m),
            RefundReason.NotDelivered, "It never arrived.", FinancialTestData.Now);

        request.Approve(Staff, null, FinancialTestData.Now);

        var reopen = () => request.ReopenOnAppeal(FinancialTestData.Now);

        reopen.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.RefundRequestNotDecidable);
    }

    private static RefundRequest Refused(Guid refuser)
    {
        var request = RefundRequest.Raise(
            RefundRequestId.New(), PaymentIntentId.New(), Member, FinancialTestData.Gbp_(20m),
            RefundReason.NotDelivered, "It never arrived.", FinancialTestData.Now);

        request.Reject(refuser, "The courier's record shows it was delivered.", FinancialTestData.Now);

        return request;
    }
}
