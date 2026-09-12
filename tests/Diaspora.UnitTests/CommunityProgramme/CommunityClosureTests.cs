using Diaspora.Modules.CommunityProgramme.Domain.Communities;
using Diaspora.Modules.CommunityProgramme.Domain.Errors;
using Diaspora.Modules.CommunityProgramme.Domain.Events;
using Diaspora.Modules.CommunityProgramme.Domain.Identifiers;
using Diaspora.SharedKernel.Identity;
using FluentAssertions;
using Xunit;
using CommunityAggregate = Diaspora.Modules.CommunityProgramme.Domain.Communities.Community;

namespace Diaspora.UnitTests.CommunityProgramme;

/// <summary>
/// Closing a community, and handing one over — the product owner's decision of 11 Sep 2026.
/// <para>
/// "It takes 30 days before this can be deleted and till then all activities in the community will
/// continue going on the normal way, and they need to provide a reason, this will be reviewed by the
/// diaspora admins… because a community might have grown so large that you cannot just delete them."
/// </para>
/// <para>
/// Four facts are kept apart here, and each test below is about the seam between two of them: asking
/// is not deciding, deciding is not closing, the wait is not optional, and handing a community on is
/// an offer somebody accepts rather than an appointment.
/// </para>
/// </summary>
public sealed class CommunityClosureTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);
    private const int WaitDays = 30;
    private const string Reason = "We started this by mistake and it has never been used.";

    // ── Asking ──────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_request_waits_thirty_days_and_changes_nothing_about_the_community()
    {
        var community = Open();

        var request = CommunityDeletionRequest.Open(
            CommunityDeletionRequestId.New(), community, UserId.New(), Reason, Now, WaitDays).Value;

        request.Status.Should().Be(CommunityDeletionStatus.AwaitingReview);
        request.EffectiveAt.Should().Be(Now.AddDays(30));

        community.Status.Should().Be(
            CommunityStatus.Active,
            "everything in the community carries on as normal while the wait runs");
        community.IsJoinable.Should().BeTrue();
    }

    [Fact]
    public void A_request_says_why()
    {
        var community = Open();

        foreach (var empty in new[] { null, "", "   ", "no" })
        {
            CommunityDeletionRequest.Open(
                    CommunityDeletionRequestId.New(), community, UserId.New(), empty, Now, WaitDays)
                .Error.Should().Be(CommunityErrors.DeletionNeedsReason);
        }
    }

    [Fact]
    public void A_request_announces_itself_to_nobody_but_carries_what_a_reviewer_needs()
    {
        var community = Open();
        var owner = UserId.New();

        var request = CommunityDeletionRequest.Open(
            CommunityDeletionRequestId.New(), community, owner, Reason, Now, WaitDays).Value;

        var raised = request.DomainEvents.OfType<CommunityDeletionRequestedDomainEvent>().Single();

        raised.Reason.Should().Be(Reason);
        raised.RequestedBy.Should().Be(owner);
        raised.EffectiveAt.Should().Be(Now.AddDays(30));
    }

    [Fact]
    public void A_closed_community_cannot_be_asked_to_close_again()
    {
        var community = Open();
        community.Archive("Closed earlier.").IsSuccess.Should().BeTrue();

        CommunityDeletionRequest.Open(
                CommunityDeletionRequestId.New(), community, UserId.New(), Reason, Now, WaitDays)
            .Error.Should().Be(CommunityErrors.ArchivedIsFinal);
    }

    // ── Deciding ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_approval_does_not_close_anything_yet()
    {
        var request = Requested(out var community);

        request.Approve(UserId.New(), null, Now.AddDays(2), community.Name).IsSuccess
            .Should().BeTrue();

        request.Status.Should().Be(CommunityDeletionStatus.Approved);
        request.IsDue(Now.AddDays(29)).Should().BeFalse("the wait is thirty days, not two");
        request.IsDue(Now.AddDays(30)).Should().BeTrue();
        community.Status.Should().Be(CommunityStatus.Active);
    }

    [Fact]
    public void A_refusal_has_to_say_why()
    {
        var request = Requested(out var community);

        request.Reject(UserId.New(), "  ", Now, community.Name).Error
            .Should().Be(CommunityErrors.DeletionDecisionNeedsReason);

        request.Status.Should().Be(CommunityDeletionStatus.AwaitingReview);
    }

    [Fact]
    public void A_refusal_reaches_the_owner_in_the_administrators_own_words()
    {
        var request = Requested(out var community);
        var administrator = UserId.New();
        const string Because = "This community has 4,000 members who post every day.";

        request.Reject(administrator, Because, Now.AddDays(3), community.Name).IsSuccess
            .Should().BeTrue();

        request.Status.Should().Be(CommunityDeletionStatus.Rejected);
        request.DecisionReason.Should().Be(Because);
        request.ReviewedBy.Should().Be(administrator);

        var decided = request.DomainEvents.OfType<CommunityDeletionDecidedDomainEvent>().Single();

        decided.Approved.Should().BeFalse();
        decided.DecisionReason.Should().Be(Because, "the owner is shown this verbatim");
        decided.OwnerUserId.Should().Be(request.RequestedBy);
    }

    [Fact]
    public void A_decision_is_made_once()
    {
        var request = Requested(out var community);
        request.Approve(UserId.New(), null, Now, community.Name).IsSuccess.Should().BeTrue();

        request.Reject(UserId.New(), "Changed my mind.", Now, community.Name).Error
            .Should().Be(CommunityErrors.DeletionAlreadyDecided);
    }

    // ── The wait running out ────────────────────────────────────────────────────────────────

    [Fact]
    public void Nothing_closes_before_the_wait_is_over()
    {
        var request = Requested(out var community);
        request.Approve(UserId.New(), null, Now, community.Name).IsSuccess.Should().BeTrue();

        request.Complete(Now.AddDays(29)).Error.Should().Be(CommunityErrors.DeletionNotDue);
        request.Status.Should().Be(CommunityDeletionStatus.Approved);
    }

    [Fact]
    public void A_request_nobody_reviewed_is_never_carried_out()
    {
        // The point of a review. A deletion that happened because nobody looked would be the
        // opposite of one.
        var request = Requested(out _);

        request.IsDue(Now.AddYears(1)).Should().BeFalse();
        request.Complete(Now.AddYears(1)).Error.Should().Be(CommunityErrors.DeletionNotDue);
    }

    [Fact]
    public void Closing_archives_the_community_and_says_so_once()
    {
        var request = Requested(out var community);
        request.Approve(UserId.New(), null, Now, community.Name).IsSuccess.Should().BeTrue();

        var closed = community.CloseForDeletion(request.RequestedBy, request.Reason);

        closed.IsSuccess.Should().BeTrue();
        community.Status.Should().Be(CommunityStatus.Archived);
        community.StatusReason.Should().Be(Reason);
        community.IsJoinable.Should().BeFalse();
        community.DomainEvents.OfType<CommunityDeletedDomainEvent>().Should().ContainSingle();

        // Nothing is erased: what people wrote stays readable, and closing twice announces nothing.
        community.CloseForDeletion(request.RequestedBy, request.Reason).Error
            .Should().Be(CommunityErrors.ArchivedIsFinal);

        request.Complete(Now.AddDays(30)).IsSuccess.Should().BeTrue();
        request.Status.Should().Be(CommunityDeletionStatus.Completed);
    }

    [Fact]
    public void An_owner_can_change_their_mind_while_it_waits()
    {
        var request = Requested(out var community);
        request.Approve(UserId.New(), null, Now, community.Name).IsSuccess.Should().BeTrue();

        request.Withdraw(Now.AddDays(10)).IsSuccess.Should().BeTrue();

        request.Status.Should().Be(CommunityDeletionStatus.Withdrawn);
        request.IsDue(Now.AddDays(40)).Should().BeFalse();
    }

    // ── Handing it over ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_offer_is_pending_until_the_other_person_answers()
    {
        var community = Open();
        var owner = UserId.New();
        var moderator = UserId.New();

        var transfer = CommunityOwnershipTransfer.Offer(
            CommunityOwnershipTransferId.New(), community, owner, moderator,
            "You have run this for two years.", Now, 14).Value;

        transfer.Status.Should().Be(CommunityOwnershipTransferStatus.Offered);
        transfer.ExpiresAt.Should().Be(Now.AddDays(14));

        var offered = transfer.DomainEvents.OfType<CommunityOwnershipOfferedDomainEvent>().Single();
        offered.OfferedTo.Should().Be(moderator);
        offered.CommunitySlug.Should().Be(community.Slug.Value);
    }

    [Fact]
    public void Nobody_offers_a_community_to_themselves()
    {
        var community = Open();
        var owner = UserId.New();

        CommunityOwnershipTransfer.Offer(
                CommunityOwnershipTransferId.New(), community, owner, owner, null, Now, 14)
            .Error.Should().Be(CommunityErrors.OwnershipOfferToSelf);
    }

    [Fact]
    public void An_offer_is_answered_once()
    {
        var transfer = Offered(out var community);

        transfer.Accept(Now.AddDays(1), community.Name).IsSuccess.Should().BeTrue();
        transfer.Status.Should().Be(CommunityOwnershipTransferStatus.Accepted);

        transfer.Decline(Now.AddDays(2), null, community.Name).Error
            .Should().Be(CommunityErrors.OwnershipOfferAlreadyAnswered);
    }

    [Fact]
    public void An_offer_that_ran_out_of_time_is_answered_by_nobody()
    {
        var transfer = Offered(out var community);

        transfer.Accept(Now.AddDays(15), community.Name).Error
            .Should().Be(CommunityErrors.OwnershipOfferExpired);

        transfer.Expire(Now.AddDays(15)).IsSuccess.Should().BeTrue();
        transfer.Status.Should().Be(CommunityOwnershipTransferStatus.Expired);
    }

    [Fact]
    public void An_offer_still_in_time_cannot_be_expired_by_the_sweep()
    {
        var transfer = Offered(out _);

        transfer.Expire(Now.AddDays(13)).Error
            .Should().Be(CommunityErrors.OwnershipOfferAlreadyAnswered);
    }

    [Fact]
    public void A_refusal_reaches_the_owner_with_whatever_reason_was_given()
    {
        var transfer = Offered(out var community);

        transfer.Decline(Now.AddDays(1), "I do not have the time.", community.Name).IsSuccess
            .Should().BeTrue();

        var decided = transfer.DomainEvents.OfType<CommunityOwnershipDecidedDomainEvent>().Single();

        decided.Accepted.Should().BeFalse();
        decided.DeclineReason.Should().Be("I do not have the time.");
        decided.OfferedBy.Should().Be(transfer.OfferedBy);
    }

    // ── What accepting does to the two memberships ──────────────────────────────────────────

    [Fact]
    public void The_new_owner_owns_and_administers_and_the_old_one_still_administers()
    {
        var community = Open();
        var founder = CommunityMembership.Found(
            CommunityMembershipId.New(), community, UserId.New(), Now).Value;

        var successor = Member(community);
        successor.Grant(CommunityMembershipRole.Moderator).IsSuccess.Should().BeTrue();

        founder.StepDownAsOwner().IsSuccess.Should().BeTrue();
        successor.AppointOwner().IsSuccess.Should().BeTrue();

        successor.Holds(CommunityMembershipRole.Owner).Should().BeTrue();
        successor.Holds(CommunityMembershipRole.Administrator).Should().BeTrue();

        founder.Holds(CommunityMembershipRole.Owner).Should().BeFalse();
        founder.Holds(CommunityMembershipRole.Administrator).Should().BeTrue(
            "the person who founded a community usually knows how it is run");

        var set = CommunityCapabilitySet.For(community, founder);

        set.CanManageRoles.Should().BeTrue();
        set.CanAppointAdministrators.Should().BeFalse("that is the owner's, and they are not");
        set.CanLeave.Should().BeTrue("having handed it on, they may now leave — as owner they could not");
    }

    [Fact]
    public void Somebody_who_does_not_own_it_cannot_step_down_from_owning_it()
    {
        var community = Open();
        var member = Member(community);

        member.StepDownAsOwner().Error.Should().Be(CommunityErrors.NotTheOwner);
    }

    // ── Who is offered any of this ──────────────────────────────────────────────────────────

    [Fact]
    public void Only_the_owner_is_offered_closing_or_handing_over()
    {
        var community = Open();
        var founder = CommunityMembership.Found(
            CommunityMembershipId.New(), community, UserId.New(), Now).Value;

        var administrator = Member(community);
        administrator.Grant(CommunityMembershipRole.Administrator).IsSuccess.Should().BeTrue();

        var owner = CommunityCapabilitySet.For(community, founder);
        owner.CanRequestDeletion.Should().BeTrue();
        owner.CanTransferOwnership.Should().BeTrue();

        var admin = CommunityCapabilitySet.For(community, administrator);
        admin.CanRequestDeletion.Should().BeFalse();
        admin.CanTransferOwnership.Should().BeFalse();

        var stranger = CommunityCapabilitySet.For(community, null);
        stranger.CanRequestDeletion.Should().BeFalse();
        stranger.CanTransferOwnership.Should().BeFalse();
    }

    [Fact]
    public void Neither_is_offered_once_the_community_has_closed()
    {
        var community = Open();
        var founder = CommunityMembership.Found(
            CommunityMembershipId.New(), community, UserId.New(), Now).Value;

        community.Archive("Closed.").IsSuccess.Should().BeTrue();

        var set = CommunityCapabilitySet.For(community, founder);

        set.CanRequestDeletion.Should().BeFalse();
        set.CanTransferOwnership.Should().BeFalse();
    }

    // ── Builders ────────────────────────────────────────────────────────────────────────────

    private static CommunityAggregate Open()
    {
        var community = CommunityAggregate.Propose(
            CommunityId.From(Guid.NewGuid()),
            CommunitySlug.FromName("Nigerians in Leeds").Value,
            "Nigerians in Leeds",
            CommunityType.NationalDiaspora,
            CommunityVisibility.Public,
            "NG").Value;

        community.Open().IsSuccess.Should().BeTrue();
        return community;
    }

    private static CommunityMembership Member(CommunityAggregate community)
    {
        var member = CommunityMembership.Start(
            CommunityMembershipId.New(), community.Id, UserId.New(),
            CommunityMembershipState.Discovering).Value;

        member.Join(community, requiresApproval: false, Now).IsSuccess.Should().BeTrue();
        member.SetVisibility(CommunityMembershipVisibility.CommunityOnly);
        return member;
    }

    private static CommunityDeletionRequest Requested(out CommunityAggregate community)
    {
        community = Open();

        return CommunityDeletionRequest.Open(
            CommunityDeletionRequestId.New(), community, UserId.New(), Reason, Now, WaitDays).Value;
    }

    private static CommunityOwnershipTransfer Offered(out CommunityAggregate community)
    {
        community = Open();

        return CommunityOwnershipTransfer.Offer(
            CommunityOwnershipTransferId.New(), community, UserId.New(), UserId.New(),
            null, Now, 14).Value;
    }
}
