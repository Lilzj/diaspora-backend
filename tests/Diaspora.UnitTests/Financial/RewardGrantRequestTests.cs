using Diaspora.Domain.Abstractions;
using Diaspora.Modules.Financial;
using Diaspora.Modules.Financial.Domain.Enums;
using Diaspora.Modules.Financial.Domain.Identifiers;
using Diaspora.Modules.Financial.Domain.Rewards;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Financial;

/// <summary>
/// A staff grant waiting for a second Admin (RWD-10): asked for once, decided once, and the grant it
/// leads to names both hands.
/// </summary>
public class RewardGrantRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 27, 12, 0, 0, TimeSpan.Zero);

    private static readonly Guid Member = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly Guid Asker = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static readonly Guid Approver = Guid.Parse("33333333-3333-3333-3333-333333333333");

    private static RewardGrantRequest Asked() => RewardGrantRequest.Raise(
        RewardGrantRequestId.New(), Member, 800, " StaffAdjustment ", " grant:prize-5120 ", " Event prize ", Asker, Now);

    [Fact]
    public void A_request_starts_pending_and_names_nobody_but_the_asker()
    {
        var request = Asked();

        request.Status.Should().Be(RewardGrantRequestStatus.Pending);
        request.IsPending.Should().BeTrue();
        request.RequestedByUserId.Should().Be(Asker);
        request.DecidedByUserId.Should().BeNull();
        request.GrantReference.Should().Be("grant:prize-5120");
        request.Reason.Should().Be("Event prize");
    }

    [Fact]
    public void An_approval_records_the_approver_and_the_policy_that_allowed_it()
    {
        var request = Asked();

        request.Approve(Approver, note: null, "reward.grant@2", Now.AddHours(1));

        request.Status.Should().Be(RewardGrantRequestStatus.Approved);
        request.DecidedByUserId.Should().Be(Approver);
        request.DecidedAt.Should().Be(Now.AddHours(1));
        request.PolicyVersion.Should().Be("reward.grant@2");
        request.Revision.Should().Be(1);
    }

    [Fact]
    public void A_withdrawal_names_no_decider_and_keeps_its_note()
    {
        var request = Asked();

        request.Withdraw("Raised against the wrong member.", Now);

        request.Status.Should().Be(RewardGrantRequestStatus.Withdrawn);
        request.DecidedByUserId.Should().BeNull();
        request.DecisionNote.Should().Be("Raised against the wrong member.");
    }

    [Fact]
    public void A_decided_request_cannot_be_decided_again()
    {
        var request = Asked();
        request.Reject(Approver, "No ticket behind it.", Now);

        var again = () => request.Approve(Approver, null, "reward.grant@2", Now);

        again.Should().Throw<DomainException>()
            .Which.Code.Should().Be(FinancialErrorCodes.RewardGrantRequestDecided);
    }

    [Fact]
    public void The_grant_an_approval_makes_names_both_hands()
    {
        var account = RewardAccount.Open(RewardAccountId.New(), Member, Now);

        account.Grant(
            Guid.NewGuid(), 800, "StaffAdjustment", "grant:prize-5120", Now,
            grantedByUserId: Asker, reason: "Event prize", policyVersion: "reward.grant@2",
            vestsAt: Now.AddDays(7), approvedByUserId: Approver);

        var entry = account.Entries.Should().ContainSingle().Subject;
        entry.GrantedByUserId.Should().Be(Asker);
        entry.ApprovedByUserId.Should().Be(Approver);
        account.PendingBalance.Should().Be(800);
    }
}
