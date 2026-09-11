using System.Text.RegularExpressions;
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
/// Community administration — the product owner's decision of 11 Sep 2026, as assertions.
/// <para>
/// "The person that creates the community is the owner of the community and can also assign roles
/// to members, and a community creator is automatically member of the administrator."
/// </para>
/// <para>
/// Until then the roles, <c>Grant</c> and <c>RevokeRole</c> all existed and nothing called any of
/// them, so every community had members and nobody who ran it. These pin what running one means.
/// </para>
/// </summary>
public sealed class CommunityAdministrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 11, 12, 0, 0, TimeSpan.Zero);

    // ── Founding ────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void The_founder_owns_and_administers_from_the_first_moment()
    {
        var community = Proposed();

        var founder = CommunityMembership.Found(
            CommunityMembershipId.New(), community, UserId.New(), Now).Value;

        founder.IsCurrentMember.Should().BeTrue(
            "the founder belongs, even though a pending community is not joinable by anybody else");
        founder.Holds(CommunityMembershipRole.Owner).Should().BeTrue();
        founder.Holds(CommunityMembershipRole.Administrator).Should().BeTrue(
            "the decision: a community creator is automatically in the administrator group");

        var set = CommunityCapabilitySet.For(community, founder);

        set.CanManageRoles.Should().BeTrue();
        set.CanAppointAdministrators.Should().BeTrue();
    }

    [Fact]
    public void The_founder_is_visible_to_the_community_rather_than_private()
    {
        // Founding is an explicit, public act. An owner the members cannot see is one they cannot
        // contact or hold to account.
        var founder = CommunityMembership.Found(
            CommunityMembershipId.New(), Proposed(), UserId.New(), Now).Value;

        founder.Visibility.Should().Be(CommunityMembershipVisibility.CommunityOnly);
    }

    [Fact]
    public void Nobody_else_can_join_a_pending_community()
    {
        // The domain's own rule, and the answer to name-squatting: a proposed community is not open
        // until staff open it. Founding is the one exception, and it is the founder's alone.
        var community = Proposed();

        var stranger = CommunityMembership.Start(
            CommunityMembershipId.New(), community.Id, UserId.New(),
            CommunityMembershipState.Discovering).Value;

        stranger.Join(community, requiresApproval: false, Now).Error
            .Should().Be(CommunityErrors.NotJoinable);
    }

    // ── Who may do what ─────────────────────────────────────────────────────────────────────

    [Fact]
    public void An_administrator_manages_roles_and_cannot_appoint_administrators()
    {
        var (community, member) = VisibleMember();
        member.Grant(CommunityMembershipRole.Administrator).IsSuccess.Should().BeTrue();

        var set = CommunityCapabilitySet.For(community, member);

        set.CanManageRoles.Should().BeTrue("administrators give moderator and ambassador roles");
        set.CanAppointAdministrators.Should().BeFalse(
            "an administrator who could make administrators could hand the community to anybody");
    }

    [Fact]
    public void An_owner_always_administers_even_if_the_administrator_entry_were_missing()
    {
        // The capability set reads Owner as administering on its own. The refusal in RevokeRole is
        // the first line; this is the second.
        var (community, member) = VisibleMember();
        member.AppointOwner().IsSuccess.Should().BeTrue();

        CommunityCapabilitySet.For(community, member).CanManageRoles.Should().BeTrue();
    }

    [Fact]
    public void The_owner_keeps_administration()
    {
        var founder = CommunityMembership.Found(
            CommunityMembershipId.New(), Proposed(), UserId.New(), Now).Value;

        founder.RevokeRole(CommunityMembershipRole.Administrator).Error
            .Should().Be(CommunityErrors.OwnerKeepsAdministration);

        founder.Holds(CommunityMembershipRole.Administrator).Should().BeTrue();
    }

    [Fact]
    public void Ownership_is_not_removed_through_the_role_route()
    {
        var founder = CommunityMembership.Found(
            CommunityMembershipId.New(), Proposed(), UserId.New(), Now).Value;

        founder.RevokeRole(CommunityMembershipRole.Owner).Error
            .Should().Be(CommunityErrors.OwnerRoleNotAssignable);

        founder.Holds(CommunityMembershipRole.Owner).Should().BeTrue();
    }

    [Fact]
    public void A_role_that_was_granted_can_be_revoked()
    {
        var (_, member) = VisibleMember();
        member.Grant(CommunityMembershipRole.Moderator).IsSuccess.Should().BeTrue();

        member.RevokeRole(CommunityMembershipRole.Moderator).IsSuccess.Should().BeTrue();
        member.Holds(CommunityMembershipRole.Moderator).Should().BeFalse();
    }

    // ── Privacy ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_private_member_cannot_be_given_a_role()
    {
        // A role shows other members that you belong. Somebody who kept their membership private
        // made a choice §41 protects, and a role would undo it without asking.
        var (community, member) = VisibleMember();
        member.SetVisibility(CommunityMembershipVisibility.Private);

        member.Grant(CommunityMembershipRole.Moderator).Error
            .Should().Be(CommunityErrors.RoleNeedsVisibleMembership);

        CommunityCapabilitySet.For(community, member).CanModerateContent.Should().BeFalse();
    }

    [Fact]
    public void A_private_member_cannot_be_appointed_owner_either()
    {
        var (_, member) = VisibleMember();
        member.SetVisibility(CommunityMembershipVisibility.Private);

        member.AppointOwner().Error.Should().Be(CommunityErrors.RoleNeedsVisibleMembership);
    }

    // ── The milestone ───────────────────────────────────────────────────────────────────────

    [Fact]
    public void A_milestone_is_raised_when_the_count_reaches_it()
    {
        var community = Proposed();

        for (var i = 0; i < 9; i++)
        {
            community.RecordMemberJoined();
        }

        community.DomainEvents.OfType<CommunityMilestoneReachedDomainEvent>().Should().BeEmpty();

        community.RecordMemberJoined();

        var reached = community.DomainEvents.OfType<CommunityMilestoneReachedDomainEvent>().Single();
        reached.Milestone.Should().Be(10);
        reached.MemberCount.Should().Be(10);
        reached.CommunityName.Should().Be(community.Name);
    }

    [Fact]
    public void A_milestone_is_reached_once_however_often_the_count_crosses_it()
    {
        // 10, down to 9, back to 10. The community has not reached ten members twice, and a notice
        // that said so would teach its administrators to ignore the next one.
        var community = Proposed();

        for (var i = 0; i < 10; i++)
        {
            community.RecordMemberJoined();
        }

        community.RecordMemberLeft();
        community.RecordMemberJoined();

        community.DomainEvents.OfType<CommunityMilestoneReachedDomainEvent>()
            .Should().ContainSingle("ten was reached once");

        community.HighestMemberMilestone.Should().Be(10);
    }

    [Fact]
    public void The_migration_backfills_with_the_same_milestones_the_domain_uses()
    {
        // A backfill with a different list would leave existing communities either announcing a
        // milestone they passed long ago or never announcing one they have not.
        var migration = Directory
            .EnumerateFiles(
                Path.Combine(RepositoryRoot(), "src", "Diaspora.Persistence", "Migrations"),
                "*_CommunityMemberMilestone.cs")
            .Single();

        var backfilled = Regex
            .Matches(File.ReadAllText(migration), @">= (\d+) THEN")
            .Select(match => int.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture))
            .OrderBy(value => value)
            .ToArray();

        backfilled.Should().Equal(CommunityAggregate.MemberMilestones);
    }

    // ── Builders ────────────────────────────────────────────────────────────────────────────

    private static CommunityAggregate Proposed() =>
        CommunityAggregate.Propose(
            CommunityId.From(Guid.NewGuid()),
            CommunitySlug.FromName("Nigerians in Leeds").Value,
            "Nigerians in Leeds",
            CommunityType.NationalDiaspora,
            CommunityVisibility.Public,
            "NG").Value;

    private static (CommunityAggregate Community, CommunityMembership Member) VisibleMember()
    {
        var community = Proposed();
        community.Open().IsSuccess.Should().BeTrue();

        var member = CommunityMembership.Start(
            CommunityMembershipId.New(), community.Id, UserId.New(),
            CommunityMembershipState.Discovering).Value;

        member.Join(community, requiresApproval: false, Now).IsSuccess.Should().BeTrue();
        member.SetVisibility(CommunityMembershipVisibility.CommunityOnly);

        return (community, member);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Diaspora.slnx"))
               && !Directory.Exists(Path.Combine(directory.FullName, ".git")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("The repository root could not be found.");
    }
}
