using Diaspora.Application.Identity.Administration;
using Diaspora.Domain.Identity.AccessStates;
using Diaspora.Domain.Identity.Administration;
using Diaspora.Domain.Identity.Events;
using Diaspora.Domain.Identity.Users;
using Diaspora.SharedKernel.Identity;
using Diaspora.UnitTests.Identity.TestData;
using FluentAssertions;
using Xunit;

namespace Diaspora.UnitTests.Identity.Domain;

/// <summary>
/// The owner's rule for staff acting on accounts (7 Nov 2026): rank, a second member of staff,
/// reasons the member is told, and a suspension that ends on its date.
/// </summary>
public class StaffActionRulesTests
{
    private static readonly DateTimeOffset Now = IdentityTestData.Now;

    [Theory]
    [InlineData("Moderator", "CommunityUser", true)]
    [InlineData("Moderator", "ServiceProvider", true)]
    [InlineData("Moderator", "Moderator", false)]
    [InlineData("Moderator", "Admin", false)]
    [InlineData("Admin", "Moderator", true)]
    [InlineData("Admin", "Admin", false)]
    [InlineData("SuperAdmin", "Admin", true)]
    [InlineData("SuperAdmin", "SuperAdmin", false)]
    [InlineData("CommunityUser", "Guest", false)]
    public void Staff_outrank_only_the_roles_strictly_below_them(string actor, string target, bool outranks)
    {
        RoleGrants.Parse(actor)!.Outranks(RoleGrants.Parse(target)!).Should().Be(outranks);
    }

    [Fact]
    public void Nobody_may_act_on_their_own_account_whatever_their_rank()
    {
        var superAdmin = IdentityTestData.VerifiedUser();
        superAdmin.AssignRole(UserRole.SuperAdmin, "Founder.", Now);

        var result = StaffAuthority.MayActOn(superAdmin, superAdmin);

        result.Error.Code.Should().Be("Identity.Administration.CannotActOnOwnAccount");
    }

    [Fact]
    public void A_moderator_may_not_act_on_an_admin()
    {
        var moderator = IdentityTestData.VerifiedUser();
        moderator.AssignRole(UserRole.Moderator, "Joining moderation.", Now);
        var admin = IdentityTestData.VerifiedUser();
        admin.AssignRole(UserRole.Admin, "Running the platform.", Now);

        StaffAuthority.MayActOn(moderator, admin).Error.Code
            .Should().Be("Identity.Administration.TargetNotBelowYourRank");
        StaffAuthority.MayActOn(admin, moderator).IsSuccess.Should().BeTrue();
    }

    [Theory]
    [InlineData("Admin", true)]
    [InlineData("SuperAdmin", true)]
    [InlineData("Moderator", false)]
    [InlineData("CommunityUser", false)]
    [InlineData("Moderatorr", false)]
    public void Only_granting_admin_or_super_admin_needs_a_second_member_of_staff(string role, bool needsSecond)
    {
        RoleGrants.NeedsSecondStaff(role).Should().Be(needsSecond);
    }

    [Fact]
    public void A_role_change_is_announced_with_the_reason_and_a_repeat_is_not()
    {
        var user = IdentityTestData.VerifiedUser();
        user.ClearDomainEvents();

        user.AssignRole(UserRole.Moderator, "Joining the moderation team.", Now);
        user.AssignRole(UserRole.Moderator, "Again.", Now);

        var changed = user.DomainEvents.OfType<UserRoleChangedDomainEvent>().Should().ContainSingle().Subject;
        changed.PreviousRole.Should().Be(nameof(UserRole.CommunityUser));
        changed.NewRole.Should().Be(nameof(UserRole.Moderator));
        changed.Reason.Should().Be("Joining the moderation team.");
    }

    [Fact]
    public void A_suspension_keeps_its_reason_code_until_it_is_lifted_and_the_lifting_note_is_what_is_announced()
    {
        var user = IdentityTestData.VerifiedUser();
        user.Suspend("Asked buyers to pay outside the platform.", Now.AddDays(7), Now, "FraudOrScam");

        user.StatusReasonCode.Should().Be("FraudOrScam");
        user.DomainEvents.OfType<UserAccessStateChangedDomainEvent>().Last().ReasonCode.Should().Be("FraudOrScam");

        user.ClearDomainEvents();
        user.Reinstate(Now, AccessStateChangeReason.ModerationReinstatement, "The reports were withdrawn.")
            .IsSuccess.Should().BeTrue();

        user.AccessState.Should().Be(AccessState.Verified);
        user.StatusReasonCode.Should().BeNull();
        user.StatusReason.Should().BeNull();
        user.SuspendedUntil.Should().BeNull();

        // Until 7 Nov 2026 the reinstatement notice repeated the suspension's own reason.
        var lifted = user.DomainEvents.OfType<UserAccessStateChangedDomainEvent>().Should().ContainSingle().Subject;
        lifted.Notes.Should().Be("The reports were withdrawn.");
        lifted.ReasonCode.Should().BeNull();
    }

    [Fact]
    public void A_lock_on_a_suspended_account_does_not_inherit_the_suspensions_reason_code()
    {
        var user = IdentityTestData.VerifiedUser();
        user.Suspend("Spam in every group.", null, Now, "Spam");

        user.Lock("Suspected compromise", Now);

        user.StatusReasonCode.Should().BeNull();
    }

    [Fact]
    public void Reopening_a_closed_account_returns_it_at_limited_access_with_the_note_announced()
    {
        var user = IdentityTestData.VerifiedUser();
        user.Disable("Posing as the consulate.", Now, "Impersonation");
        user.ClearDomainEvents();

        user.Reactivate(Now, "Appeal upheld.").IsSuccess.Should().BeTrue();

        user.AccessState.Should().Be(AccessState.LimitedAccess);
        user.StatusReasonCode.Should().BeNull();
        user.DomainEvents.OfType<UserAccessStateChangedDomainEvent>().Single().Notes.Should().Be("Appeal upheld.");
    }

    // ── The proposal ────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_proposal_is_confirmed_by_a_second_member_of_staff_and_never_by_its_proposer()
    {
        var proposer = UserId.New();
        var proposal = Propose(proposer);

        proposal.Confirm(proposer, null, Now).Error.Code
            .Should().Be("Identity.Administration.ProposerCannotDecide");
        proposal.Decline(proposer, "No.", Now).Error.Code
            .Should().Be("Identity.Administration.ProposerCannotDecide");
        proposal.Status.Should().Be(AccountActionProposalStatus.Pending);

        var second = UserId.New();
        proposal.Confirm(second, "Agreed.", Now).IsSuccess.Should().BeTrue();

        proposal.Status.Should().Be(AccountActionProposalStatus.Confirmed);
        proposal.DecidedBy.Should().Be(second);
        proposal.DecisionNote.Should().Be("Agreed.");
        proposal.ProposedBy.Should().Be(proposer, "both people stay on the record");
    }

    [Fact]
    public void A_proposal_is_decided_once()
    {
        var proposal = Propose(UserId.New());
        proposal.Decline(UserId.New(), "Wrong member.", Now);

        proposal.Confirm(UserId.New(), null, Now).Error.Code
            .Should().Be("Identity.Administration.ProposalNotPending");
    }

    [Fact]
    public void Only_the_proposer_withdraws_and_only_while_it_is_open()
    {
        var proposer = UserId.New();
        var proposal = Propose(proposer);

        proposal.Withdraw(UserId.New(), null, Now).Error.Code
            .Should().Be("Identity.Administration.OnlyProposerWithdraws");

        proposal.Withdraw(proposer, "Wrong account.", Now).IsSuccess.Should().BeTrue();
        proposal.Status.Should().Be(AccountActionProposalStatus.Withdrawn);

        proposal.Withdraw(proposer, null, Now).Error.Code
            .Should().Be("Identity.Administration.ProposalNotPending");
    }

    [Fact]
    public void Only_a_suspension_carries_an_end_and_only_a_role_grant_carries_a_role()
    {
        var closure = AccountActionProposal.Propose(
            AccountActionKind.Disable, UserId.New(), UserId.New(), "Spam", "Spam in every group.",
            Now.AddDays(3), "Admin", Now);

        closure.Until.Should().BeNull();
        closure.Role.Should().BeNull();
    }

    [Fact]
    public void A_suspension_the_platform_raises_needs_a_member_of_staff_to_take_it_up_before_another_confirms()
    {
        var caseId = Guid.NewGuid();
        var raised = AccountActionProposal.Raise(
            UserId.New(), caseId, "FraudOrScam", "Third strike in jobs.", Now);

        raised.Kind.Should().Be(AccountActionKind.Suspend);
        raised.ProposedBy.Should().BeNull();
        raised.AwaitsProposer.Should().BeTrue();
        raised.RaisedByCaseId.Should().Be(caseId);
        raised.Until.Should().BeNull("a third strike suspends until reviewed");

        // The platform is not one of the two people.
        raised.Confirm(UserId.New(), null, Now).Error.Code
            .Should().Be("Identity.Administration.ProposalNeedsProposer");

        var first = UserId.New();
        raised.Adopt(first, "Three adverts asked for visa fees.", Now).IsSuccess.Should().BeTrue();

        raised.ProposedBy.Should().Be(first);
        raised.AwaitsProposer.Should().BeFalse();
        raised.Note.Should().Be("Three adverts asked for visa fees.", "the member reads the proposer's note");

        raised.Adopt(UserId.New(), "Me too.", Now).Error.Code
            .Should().Be("Identity.Administration.ProposalAlreadyHasProposer");
        raised.Confirm(first, null, Now).Error.Code
            .Should().Be("Identity.Administration.ProposerCannotDecide");

        raised.Confirm(UserId.New(), "Agreed.", Now).IsSuccess.Should().BeTrue();
        raised.Status.Should().Be(AccountActionProposalStatus.Confirmed);
    }

    [Fact]
    public void A_suspension_the_platform_raises_can_be_declined_before_anybody_takes_it_up()
    {
        var raised = AccountActionProposal.Raise(
            UserId.New(), Guid.NewGuid(), "Spam", "Third strike in community.", Now);

        raised.Decline(UserId.New(), "The removals were overturned.", Now).IsSuccess.Should().BeTrue();
        raised.Status.Should().Be(AccountActionProposalStatus.Declined);

        // Nobody proposed it, so nobody withdraws it.
        AccountActionProposal.Raise(UserId.New(), Guid.NewGuid(), "Spam", "Third strike.", Now)
            .Withdraw(UserId.New(), null, Now).Error.Code
            .Should().Be("Identity.Administration.OnlyProposerWithdraws");
    }

    private static AccountActionProposal Propose(UserId proposer) =>
        AccountActionProposal.Propose(
            AccountActionKind.Suspend,
            UserId.New(),
            proposer,
            "FraudOrScam",
            "Asked buyers to pay outside the platform.",
            Now.AddDays(7),
            role: null,
            Now);
}
